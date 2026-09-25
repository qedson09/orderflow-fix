import type { Order, OrderPage, OrderRequest, AuditResponse, ExposureResponse } from '../domain/order';
export interface OrderApi {
    submit(request: OrderRequest, signal?: AbortSignal): Promise<Order>;
    get(clOrdId: string, signal?: AbortSignal): Promise<Order>;
    list(signal?: AbortSignal, accountId?: string, page?: number, pageSize?: number): Promise<OrderPage>;
    exposures(accountId: string, signal?: AbortSignal): Promise<ExposureResponse>;
    events(clOrdId: string, signal?: AbortSignal): Promise<AuditResponse>;
}
export class ApiError extends Error {
    constructor(message: string, public status = 0, public code = 'NETWORK', public retryAfterMs = 0, public fieldErrors: Record<string, string[]> = {}) { super(message); }
}
export const messageOf = (error: unknown) => error instanceof Error ? error.message : 'Falha inesperada. Consulte novamente com o mesmo ID.';
