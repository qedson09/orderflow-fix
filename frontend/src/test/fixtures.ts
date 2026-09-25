import { vi } from 'vitest';
import type { OrderApi } from '../api/types';
import type { Order, OrderRequest } from '../domain/order';
export const request: OrderRequest = { clOrdId: 'ed0f4b17-d1ca-4f5f-9d31-f15ec0b2f155', symbol: 'PETR4', side: 'B', quantity: 100, price: '35.50' };
export const pending: Order = { ...request, status: 'Pending', createdAt: '2026-09-21T12:00:00Z', updatedAt: '2026-09-21T12:00:00Z', executionReport: null, rejectionReason: null };
export const accepted: Order = { ...pending, status: 'Accepted', executionReport: { execId: 'exec-1', execType: 'New', receivedAt: '2026-09-21T12:00:01Z' } };
export const rejected: Order = { ...pending, status: 'Rejected', rejectionReason: 'Limite excedido', executionReport: { execId: 'exec-2', execType: 'Rejected', receivedAt: '2026-09-21T12:00:01Z' } };
export function fakeApi(): OrderApi {
    return {
        submit: vi.fn().mockResolvedValue(pending), get: vi.fn().mockResolvedValue(pending), list: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
        exposures: vi.fn().mockResolvedValue({ accountId: 'CLIENTE-001', checkedAt: '2026-09-21T12:00:00Z', items: [] }),
        events: vi.fn().mockResolvedValue({ available: true, events: [] })
    };
}
