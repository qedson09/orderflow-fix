import { afterEach, expect, it, vi } from 'vitest';
import { createHttpApi } from './http';
import { accepted, pending, request } from '../test/fixtures';
afterEach(() => vi.useRealTimers());
it('envia preço invariante e valida resposta 202 pendente', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify(pending), { status: 202 }));
    expect(await createHttpApi(100, fetcher).submit(request)).toEqual(pending);
    const [url, options] = fetcher.mock.calls[0];
    expect(url).toBe('/api/orders');
    expect(JSON.parse(options.body)).toEqual(request);
    expect(options.credentials).toBe('same-origin');
});
it.each([400, 409, 429, 500, 503])('mapeia HTTP %i', async status => {
    const fetcher = vi.fn().mockResolvedValue(new Response('{}', { status, headers: { 'Retry-After': '3' } }));
    await expect(createHttpApi(100, fetcher).submit(request)).rejects.toMatchObject({ status, retryAfterMs: 3000 });
});
it('mapeia FIX_UNAVAILABLE separadamente', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response('{"code":"FIX_UNAVAILABLE"}', { status: 503 }));
    await expect(createHttpApi(100, fetcher).submit(request)).rejects.toMatchObject({ code: 'FIX_UNAVAILABLE' });
});
it('recusa status aceito sem relatório FIX no contrato', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ...accepted, executionReport: null })));
    await expect(createHttpApi(100, fetcher).get(request.clOrdId)).rejects.toMatchObject({ code: 'INVALID_RESPONSE' });
});
it('timeout aborta o fetch e retorna resultado desconhecido', async () => {
    vi.useFakeTimers();
    const fetcher = vi.fn((_url: RequestInfo | URL, init?: RequestInit) => new Promise<Response>((_resolve, reject) => {
        init?.signal?.addEventListener('abort', () => reject(new DOMException('Abortado', 'AbortError')));
    }));
    const result = expect(createHttpApi(100, fetcher).submit(request)).rejects.toMatchObject({ code: 'TIMEOUT' });
    await vi.advanceTimersByTimeAsync(100); await result;
    expect(fetcher.mock.calls[0][1]?.signal?.aborted).toBe(true);
});
it.each(['B', 'S'] as const)('serializa lado %s com um caractere no JSON HTTP', async side => {
    const payload = { ...request, side };
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ...pending, side }), { status: 202 }));
    expect((await createHttpApi(100, fetcher).submit(payload)).side).toBe(side);
    const body = JSON.parse(fetcher.mock.calls[0][1].body);
    expect(body.side).toBe(side);
    expect(body.side).toHaveLength(1);
});
it('consulta página e tamanho com filtro por cliente', async () => {
    const result = { items: [accepted], page: 2, pageSize: 10, totalCount: 11, totalPages: 2 };
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify(result)));
    expect(await createHttpApi(100, fetcher).list(undefined, 'CLIENTE-002', 2, 10)).toEqual(result);
    expect(fetcher.mock.calls[0][0]).toBe('/api/orders?page=2&pageSize=10&accountId=CLIENTE-002');
});
it('apresenta mensagens detalhadas e campos da validação HTTP 400', async () => {
    const errors = { side: ['Selecione Compra (B) ou Venda (S). O lado informado é inválido.'] };
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify({ code: 'ORDER_VALIDATION', errors }), { status: 400 }));
    await expect(createHttpApi(100, fetcher).submit(request)).rejects.toMatchObject({
        status: 400, code: 'ORDER_VALIDATION', fieldErrors: errors, message: errors.side[0],
    });
});
it('mostra detail de validação e não expõe detalhes internos de erro 500', async () => {
    const validation = vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'Quantidade deve ser inteira.' }), { status: 400 }));
    await expect(createHttpApi(100, validation).submit(request)).rejects.toThrow('Quantidade deve ser inteira.');
    const failure = vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'SQL connection secret' }), { status: 500 }));
    await expect(createHttpApi(100, failure).submit(request)).rejects.toThrow('API temporariamente indisponível.');
});
