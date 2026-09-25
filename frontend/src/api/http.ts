import { z } from 'zod';
import { orderPageSchema, orderSchema, eventsSchema, exposureSchema, type OrderRequest } from '../domain/order';
import { ApiError, type OrderApi } from './types';
const problem = z.object({ code: z.string().optional(), detail: z.string().optional(), title: z.string().optional(), errors: z.record(z.string(), z.array(z.string())).optional() });
const messages: Record<number, string> = {
    400: 'A API recusou os dados da solicitação. Confira os campos informados.',
    404: 'Ordem ainda não encontrada. O mesmo ID será preservado.',
    409: 'Este ID já está associado a outros dados. Consulte a ordem original; não altere o ID automaticamente.',
    429: 'Muitas solicitações. Aguarde antes de tentar novamente.',
};
export function createHttpApi(timeoutMs = 10000, fetcher: typeof fetch = fetch): OrderApi {
    async function request<T>(path: string, schema: z.ZodType<T>, signal?: AbortSignal, body?: OrderRequest): Promise<T> {
        const controller = new AbortController();
        let timedOut = false;
        const abort = () => controller.abort();
        signal?.addEventListener('abort', abort, { once: true });
        if (signal?.aborted) controller.abort();
        const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);
        try {
            const response = await fetcher(`/api${path}`, {
                method: body ? 'POST' : 'GET', credentials: 'same-origin', cache: 'no-store',
                signal: controller.signal, headers: { Accept: 'application/json', ...(body ? { 'Content-Type': 'application/json' } : {}) },
                ...(body ? { body: JSON.stringify(body) } : {}),
            });
            if (!response.ok) {
                const parsed = problem.safeParse(await response.json().catch(() => null));
                const code = parsed.success ? parsed.data.code : undefined;
                const errors = parsed.success ? parsed.data.errors ?? {} : {};
                const description = parsed.success && [400, 409].includes(response.status)
                    ? Object.values(errors).flat().join(' ') || parsed.data.detail || parsed.data.title : undefined;
                const retry = response.headers.get('Retry-After');
                const retryAfterMs = retry ? Math.max(0, /^\d+$/.test(retry) ? Number(retry) * 1000 : Date.parse(retry) - Date.now()) : 0;
                throw new ApiError(code === 'FIX_UNAVAILABLE' ? 'Sessão FIX indisponível. Sua solicitação mantém o mesmo ID.' :
                    description || messages[response.status] || (response.status >= 500 ? 'API temporariamente indisponível. O resultado da ordem pode ser desconhecido.' : 'Não foi possível consultar a API.'),
                    response.status, code || 'HTTP_ERROR', Number.isFinite(retryAfterMs) ? retryAfterMs : 0, errors);
            }
            const parsed = schema.safeParse(await response.json());
            if (!parsed.success) throw new ApiError('Resposta da API incompatível com o contrato. Consulte novamente; o ID foi preservado.', 0, 'INVALID_RESPONSE');
            return parsed.data;
        } catch (error) {
            if (signal?.aborted) throw new DOMException('Operação interrompida', 'AbortError');
            if (timedOut) throw new ApiError('Tempo de resposta excedido. Resultado desconhecido; consultaremos o mesmo ID.', 0, 'TIMEOUT');
            if (error instanceof ApiError) throw error;
            throw new ApiError('Falha de conexão. Resultado desconhecido; o ID foi preservado.');
        } finally { clearTimeout(timer); signal?.removeEventListener('abort', abort); }
    }
    return {
        submit: (body, signal) => request('/orders', orderSchema, signal, body),
        get: (id, signal) => request(`/orders/${encodeURIComponent(id)}`, orderSchema, signal),
        list: (signal, accountId, page = 1, pageSize = 10) => request(`/orders?page=${page}&pageSize=${pageSize}${accountId ? `&accountId=${encodeURIComponent(accountId)}` : ''}`, orderPageSchema, signal),
        exposures: (accountId, signal) => request(`/accounts/${encodeURIComponent(accountId)}/exposures`, exposureSchema, signal),
        events: (id, signal) => request(`/orders/${encodeURIComponent(id)}/events`, eventsSchema, signal),
    };
}
