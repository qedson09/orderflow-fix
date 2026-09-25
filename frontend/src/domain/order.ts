import { z } from 'zod';

export const symbols = ['PETR4', 'VALE3', 'VIIA4'] as const;
export const accounts = ['CLIENTE-001', 'CLIENTE-002'] as const;
export const requestSchema = z.object({
    clOrdId: z.uuid(),
    accountId: z.enum(accounts).optional(),
    symbol: z.enum(symbols),
    side: z.enum(['B', 'S']),
    quantity: z.number().int().min(1).max(99999),
    price: z.string().regex(/^(?:0|[1-9]\d{0,2})\.\d{2}$/)
        .refine(value => value !== '0.00', 'Preço deve ser positivo'),
});
export type OrderRequest = z.infer<typeof requestSchema>;
export const reportSchema = z.object({
    execId: z.string().min(1), execType: z.enum(['New', 'Rejected']),
    receivedAt: z.iso.datetime({ offset: true }),
});
export const orderSchema = requestSchema.extend({
    status: z.enum(['Pending', 'Accepted', 'Rejected']),
    createdAt: z.iso.datetime({ offset: true }),
    updatedAt: z.iso.datetime({ offset: true }),
    rejectionReason: z.string().nullable(),
    executionReport: reportSchema.nullable(),
}).superRefine((order, ctx) => {
    const expected = order.status === 'Accepted' ? 'New' : 'Rejected';
    if (order.status === 'Pending' ? order.executionReport !== null : order.executionReport?.execType !== expected) {
        ctx.addIssue({ code: 'custom', message: 'Status sem confirmação FIX correspondente' });
    }
});
export type Order = z.infer<typeof orderSchema>;
export const orderPageSchema = z.object({
    items: z.array(orderSchema), page: z.number().int().min(1), pageSize: z.number().int().min(1).max(100),
    totalCount: z.number().int().min(0), totalPages: z.number().int().min(0),
});
export type OrderPage = z.infer<typeof orderPageSchema>;
export const eventsSchema = z.object({
    available: z.boolean(),
    kafkaAvailable: z.boolean().optional(),
    events: z.array(z.object({
        eventId: z.uuid(), clOrdId: z.uuid(), occurredAt: z.iso.datetime({ offset: true }),
        type: z.string(), description: z.string(), source: z.string().optional(),
    })),
});
export type AuditResponse = z.infer<typeof eventsSchema>;
const decimalMoney = z.string().regex(/^-?\d+\.\d{2}$/);
export const exposureSchema = z.object({
    accountId: z.enum(accounts), checkedAt: z.iso.datetime({ offset: true }),
    items: z.array(z.object({
        accountId: z.enum(accounts), symbol: z.enum(symbols), currentExposure: decimalMoney,
        absoluteLimit: decimalMoney, availableForBuy: decimalMoney, availableForSell: decimalMoney,
        utilizationPercentage: z.number(), version: z.number(),
    })),
});
export type ExposureResponse = z.infer<typeof exposureSchema>;
export const isFinal = (order?: Order) => !!order && order.status !== 'Pending';
export function sameRequest(a: OrderRequest, b: OrderRequest) {
    return (a.accountId ?? 'CLIENTE-001') === (b.accountId ?? 'CLIENTE-001') && a.clOrdId === b.clOrdId && a.symbol === b.symbol && a.side === b.side &&
        a.quantity === b.quantity && a.price === b.price;
}
