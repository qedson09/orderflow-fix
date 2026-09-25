import { requestSchema, type OrderRequest } from '../domain/order';
export interface PendingStore {
    load(): OrderRequest | null;
    save(request: OrderRequest): void;
    clear(id: string): void;
}
export function createPendingStore(storage: Storage, namespace: string): PendingStore {
    const key = `orderflow:${namespace}:pending:v1`;
    function load() {
        const raw = storage.getItem(key);
        if (!raw) return null;
        const envelope = JSON.parse(raw);
        if (envelope.version !== 1) throw new Error('Versão desconhecida do registro local. Preserve os dados antes de recuperar a ordem.');
        const side = envelope.request?.side;
        const request = requestSchema.parse({ ...envelope.request, side: side === 'Buy' ? 'B' : side === 'Sell' ? 'S' : side });
        if (side !== request.side) storage.setItem(key, JSON.stringify({ ...envelope, request }));
        return request;
    }
    return {
        load,
        save(request) {
            const existing = load();
            if (existing && existing.clOrdId !== request.clOrdId) throw new Error('Há outra solicitação pendente neste navegador. Atualize a página para recuperá-la.');
            storage.setItem(key, JSON.stringify({ version: 1, request: requestSchema.parse(request) }));
        },
        clear(id) { if (load()?.clOrdId === id) storage.removeItem(key); },
    };
}
