import { z } from 'zod';
import { ApiError, type OrderApi } from './types';
import { accounts, symbols, orderSchema, sameRequest, type Order, type OrderRequest } from '../domain/order';
// Apenas demonstração no navegador: nenhum servidor, motor FIX ou cálculo de risco.
// Marcador conferido no build para garantir que este módulo não foi incluído.
export const MOCK_ONLY_MARKER = 'ORDERFLOW_DEVELOPMENT_ADAPTER_ONLY';
const key = 'orderflow:mock:orders:v1';
type RecordData = { order: Order; eventId: string };
export function createMockApi(storage: Storage = localStorage): OrderApi {
    const storedSchema = z.array(z.object({
        order: z.preprocess(value => {
            if (typeof value !== 'object' || value === null || !('side' in value)) return value;
            return { ...value, side: value.side === 'Buy' ? 'B' : value.side === 'Sell' ? 'S' : value.side };
        }, orderSchema), eventId: z.uuid()
    }));
    let records: RecordData[] = storedSchema.parse(JSON.parse(storage.getItem(key) || '[]'));
    const persist = () => storage.setItem(key, JSON.stringify(records));
    persist();
    function refresh() {
        records = records.map(record => {
            if (record.order.status !== 'Pending' || Date.now() - Date.parse(record.order.createdAt) < 2500) return record;
            // VIIA4 oferece um cenário previsível de rejeição simulada.
            const rejected = record.order.symbol === 'VIIA4';
            const now = new Date().toISOString();
            return {
                ...record, order: {
                    ...record.order, status: rejected ? 'Rejected' : 'Accepted', updatedAt: now,
                    rejectionReason: rejected ? 'Limite de exposição excedido (cenário simulado).' : null,
                    executionReport: { execId: `demo-${record.eventId}`, execType: rejected ? 'Rejected' : 'New', receivedAt: now }
                }
            };
        });
        persist();
    }
    async function wait(signal?: AbortSignal) {
        if (signal?.aborted) throw new DOMException('Interrompido', 'AbortError');
        await new Promise<void>((resolve, reject) => {
            const abort = () => { clearTimeout(timer); reject(new DOMException('Interrompido', 'AbortError')); };
            const timer = setTimeout(() => { signal?.removeEventListener('abort', abort); resolve(); }, 200);
            signal?.addEventListener('abort', abort, { once: true });
        });
        refresh();
    }
    const find = (id: string) => {
        const record = records.find(r => r.order.clOrdId === id);
        if (!record) throw new ApiError('Ordem não encontrada.', 404);
        return record;
    };
    return {
        async submit(request: OrderRequest, signal) {
            await wait(signal);
            const existing = records.find(r => r.order.clOrdId === request.clOrdId);
            if (existing) {
                if (!sameRequest(existing.order, request)) throw new ApiError('Conflito de identidade.', 409);
                return existing.order;
            }
            const now = new Date().toISOString();
            const order: Order = { ...request, status: 'Pending', createdAt: now, updatedAt: now, rejectionReason: null, executionReport: null };
            records.unshift({ order, eventId: crypto.randomUUID() }); persist(); return order;
        },
        async get(id, signal) { await wait(signal); return find(id).order; },
        async list(signal, accountId, page = 1, pageSize = 10) {
            await wait(signal);
            if (!Number.isInteger(page) || page < 1 || !Number.isInteger(pageSize) || pageSize < 1 || pageSize > 100)
                throw new ApiError('Paginação inválida.', 400);
            const selected = records.filter(r => !accountId || (r.order.accountId ?? 'CLIENTE-001') === accountId)
                .sort((a, b) => b.order.createdAt.localeCompare(a.order.createdAt) || b.order.clOrdId.localeCompare(a.order.clOrdId));
            const totalCount = selected.length;
            const totalPages = Math.ceil(totalCount / pageSize);
            const current = Math.min(page, Math.max(totalPages, 1));
            return { items: selected.slice((current - 1) * pageSize, current * pageSize).map(r => r.order), page: current, pageSize, totalCount, totalPages };
        },
        async exposures(accountId, signal) {
            await wait(signal);
            const account = accounts.find(a => a === accountId);
            if (!account) throw new ApiError('Cliente não encontrado.', 404);
            return {
                accountId: account, checkedAt: new Date().toISOString(), items: symbols.map(symbol => {
                    const selected = records.filter(r => (r.order.accountId ?? 'CLIENTE-001') === account && r.order.symbol === symbol && r.order.status === 'Accepted');
                    const cents = selected.reduce((total, { order }) => total + Number(order.price.replace('.', '')) * order.quantity * (order.side === 'B' ? 1 : -1), 0);
                    const limit = 10000000000;
                    const decimal = (value: number) => `${value < 0 ? '-' : ''}${Math.floor(Math.abs(value) / 100)}.${(Math.abs(value) % 100).toString().padStart(2, '0')}`;
                    return { accountId: account, symbol, currentExposure: decimal(cents), absoluteLimit: decimal(limit), availableForBuy: decimal(limit - cents), availableForSell: decimal(limit + cents), utilizationPercentage: Math.abs(cents) / limit * 100, version: selected.length };
                }).filter(item => item.version > 0)
            };
        },
        async events(id, signal) {
            await wait(signal);
            const record = find(id);
            const order = record.order;
            return {
                available: true, events: [
                    { eventId: id, clOrdId: id, occurredAt: order.createdAt, type: 'SubmissionPersisted', source: 'Generator (simulado)', description: 'Solicitação registrada (simulação).' },
                    ...(order.status === 'Pending' ? [] : [{ eventId: record.eventId.replace(/.$/, record.eventId.endsWith('0') ? '1' : '0'), clOrdId: id, occurredAt: order.updatedAt, type: 'ExecutionReportPersisted', source: 'Generator (simulado)', description: 'Confirmação FIX simulada.' }]),
                    ...(Date.now() - Date.parse(order.createdAt) < 9000 || order.status === 'Pending' ? [] : [{
                        eventId: record.eventId, clOrdId: id, occurredAt: order.updatedAt,
                        type: 'OrderDecisionRecorded', description: `Decisão ${order.status === 'Accepted' ? 'de aceite' : 'de rejeição'} registrada na auditoria simulada.`,
                    }])]
            };
        },
    };
}
