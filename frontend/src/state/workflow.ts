import { ApiError, messageOf, type OrderApi } from '../api/types';
import { isFinal, orderSchema, sameRequest, type Order, type OrderRequest } from '../domain/order';
import type { PendingStore } from './persistence';
export type Phase = 'idle' | 'sending' | 'pending' | 'unknown' | 'accepted' | 'rejected' | 'sendFailed' | 'fixUnavailable' | 'validationFailed';
export type Snapshot = { phase: Phase; request?: OrderRequest; order?: Order; message?: string; fieldErrors?: Record<string, string[]>; storageError?: string; busy: boolean; retryAt: number };

// Coordenador independente de React: persistência, confirmação HTTP/FIX e ciclo de polling.
export class OrderWorkflow {
    private state: Snapshot = { phase: 'idle', busy: false, retryAt: 0 };
    private listeners = new Set<() => void>();
    private controller?: AbortController;
    private timer?: ReturnType<typeof setTimeout>;
    private failures = 0;
    constructor(private api: OrderApi, private store: PendingStore, private interval = 2000) {
        try {
            const request = store.load();
            if (request) this.state = { ...this.state, request, phase: 'unknown', message: 'Solicitação recuperada. Consultando o resultado com o mesmo ID.' };
        } catch { this.state.storageError = 'Não foi possível ler o registro local. Preserve os dados do navegador e recupere a solicitação antes de enviar outra ordem.'; }
    }
    getSnapshot = () => this.state;
    subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
    private update(patch: Partial<Snapshot>) { this.state = { ...this.state, ...patch }; this.listeners.forEach(listener => listener()); }
    start = () => {
        this.stop();
        this.controller = new AbortController();
        if (this.state.request && this.state.phase !== 'validationFailed' && !isFinal(this.state.order)) void this.query(false);
    };
    stop = () => { this.controller?.abort(); clearTimeout(this.timer); this.update({ busy: false }); };
    private schedule() {
        clearTimeout(this.timer);
        if (!this.controller?.signal.aborted && this.state.request && this.state.phase !== 'validationFailed' && !isFinal(this.state.order)) {
            const delay = Math.max(this.interval * 2 ** Math.min(this.failures, 4), this.state.retryAt - Date.now());
            this.timer = setTimeout(() => void this.query(false), Math.min(Math.max(delay, 0), 2147483647));
        }
    }
    private accept(order: Order) {
        orderSchema.parse(order);
        if (!this.state.request || !sameRequest(order, this.state.request)) throw new ApiError('Resposta para dados diferentes. O ID foi preservado para investigação.', 409, 'IDENTITY_CONFLICT');
        this.failures = 0;
        const phase = order.status === 'Accepted' ? 'accepted' : order.status === 'Rejected' ? 'rejected' : 'pending';
        this.update({ order, phase, message: undefined, retryAt: 0 });
        if (isFinal(order)) {
            try { this.store.clear(order.clOrdId); }
            catch { this.update({ storageError: 'Decisão confirmada, mas não foi possível limpar a pendência local. Atualize para recuperá-la antes de enviar outra ordem.' }); }
        }
    }
    private failed(error: unknown, posting = false) {
        if (posting && error instanceof ApiError && error.status === 400 && error.code === 'ORDER_VALIDATION') {
            clearTimeout(this.timer);
            try { if (this.state.request) this.store.clear(this.state.request.clOrdId); }
            catch { this.update({ storageError: 'Não foi possível limpar a solicitação inválida do navegador.' }); }
            this.update({ phase: 'validationFailed', message: error.message, fieldErrors: error.fieldErrors, retryAt: 0 });
            return;
        }
        this.failures++;
        const phase: Phase = error instanceof ApiError && error.code === 'FIX_UNAVAILABLE' ? 'fixUnavailable' :
            posting && error instanceof ApiError && [400, 409, 429].includes(error.status) ? 'sendFailed' : 'unknown';
        this.update({ phase, message: messageOf(error), retryAt: Date.now() + (error instanceof ApiError ? error.retryAfterMs : 0) });
    }
    submit = async (request: OrderRequest) => {
        if (this.state.busy || this.state.storageError || (this.state.request && this.state.phase !== 'validationFailed' && !isFinal(this.state.order))) return;
        try { this.store.save(request); }
        catch (error) { this.update({ phase: 'sendFailed', message: messageOf(error), storageError: 'Envio bloqueado: não foi possível preservar a solicitação no navegador.' }); return; }
        this.update({ request, order: undefined, phase: 'sending', busy: true, message: undefined, fieldErrors: undefined, retryAt: 0 });
        const signal = this.controller?.signal;
        try { const order = await this.api.submit(request, signal); if (!signal?.aborted) this.accept(order); }
        catch (error) { if (!signal?.aborted) this.failed(error, true); }
        finally { if (!signal?.aborted) { this.update({ busy: false }); this.schedule(); } }
    };
    retry = () => this.query(true);
    private async query(resendIfMissing: boolean) {
        if (this.state.phase === 'validationFailed' || this.state.busy || !this.state.request || isFinal(this.state.order) || Date.now() < this.state.retryAt) { this.schedule(); return; }
        clearTimeout(this.timer);
        const signal = this.controller?.signal;
        const request = this.state.request;
        this.update({ busy: true });
        let posting = false;
        try {
            let order: Order;
            try { order = await this.api.get(request.clOrdId, signal); }
            catch (error) {
                if (!signal?.aborted && resendIfMissing && error instanceof ApiError && error.status === 404) {
                    posting = true;
                    order = await this.api.submit(request, signal);
                } else throw error;
            }
            if (!signal?.aborted) this.accept(order);
        } catch (error) { if (!signal?.aborted) this.failed(error, posting); }
        finally { if (!signal?.aborted) { this.update({ busy: false }); this.schedule(); } }
    }
}
