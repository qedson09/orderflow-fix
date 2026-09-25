import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OrderWorkflow } from './workflow';
import { createPendingStore } from './persistence';
import { ApiError } from '../api/types';
import { accepted, fakeApi, pending, rejected, request } from '../test/fixtures';
describe('ciclo de uma solicitação', () => {
  let api: ReturnType<typeof fakeApi>;
  let store: ReturnType<typeof createPendingStore>;
  let workflow: OrderWorkflow;
  beforeEach(() => { vi.useFakeTimers(); api = fakeApi(); store = createPendingStore(localStorage, 'test'); workflow = new OrderWorkflow(api, store, 100); workflow.start(); });
  afterEach(() => { workflow.stop(); vi.useRealTimers(); });
  it.each([accepted, rejected])('passa de pendente para $status apenas com confirmação FIX', async final => {
    await workflow.submit(request);
    expect(workflow.getSnapshot().phase).toBe('pending');
    expect(store.load()).toEqual(request);
    vi.mocked(api.get).mockResolvedValue(final);
    await vi.advanceTimersByTimeAsync(100);
    expect(workflow.getSnapshot().phase).toBe(final === accepted ? 'accepted' : 'rejected');
    expect(store.load()).toBeNull();
    await vi.advanceTimersByTimeAsync(1000);
    expect(api.get).toHaveBeenCalledTimes(1);
  });
  it('timeout no POST é seguido por consulta, sem outra identidade ou POST automático', async () => {
    vi.mocked(api.submit).mockRejectedValue(new ApiError('Timeout', 0, 'TIMEOUT'));
    await workflow.submit(request);
    expect(workflow.getSnapshot().phase).toBe('unknown');
    vi.mocked(api.get).mockResolvedValue(accepted);
    await vi.advanceTimersByTimeAsync(200);
    expect(api.get).toHaveBeenCalledWith(request.clOrdId, expect.any(AbortSignal));
    expect(api.submit).toHaveBeenCalledTimes(1);
    expect(workflow.getSnapshot().phase).toBe('accepted');
  });
  it('reenvia exatamente os mesmos dados e ID quando a consulta retorna 404', async () => {
    vi.mocked(api.submit).mockRejectedValueOnce(new ApiError('Conexão perdida'));
    await workflow.submit(request);
    vi.mocked(api.get).mockRejectedValue(new ApiError('Não encontrada', 404));
    await workflow.retry();
    expect(vi.mocked(api.submit).mock.calls.map(call => call[0])).toEqual([request, request]);
    expect(workflow.getSnapshot().phase).toBe('pending');
  });
  it('recupera após reload e consulta antes de tentar reenviar', async () => {
    store.save(request); workflow.stop();
    vi.mocked(api.get).mockResolvedValue(accepted);
    workflow = new OrderWorkflow(api, store, 100); workflow.start();
    await vi.advanceTimersByTimeAsync(0);
    expect(api.submit).not.toHaveBeenCalled();
    expect(workflow.getSnapshot().phase).toBe('accepted');
  });
  it('não permite nova identidade enquanto o resultado estiver desconhecido', async () => {
    vi.mocked(api.submit).mockRejectedValue(new ApiError('Offline'));
    await workflow.submit(request);
    await workflow.submit({ ...request, clOrdId: crypto.randomUUID() });
    expect(api.submit).toHaveBeenCalledTimes(1);
    expect(store.load()).toEqual(request);
  });
  it('preserva a solicitação se FIX estiver indisponível', async () => {
    vi.mocked(api.submit).mockRejectedValue(new ApiError('Sessão indisponível', 503, 'FIX_UNAVAILABLE'));
    await workflow.submit(request);
    expect(workflow.getSnapshot().phase).toBe('fixUnavailable');
    expect(store.load()).toEqual(request);
  });
  it.each([400, 409, 429])('distingue falha HTTP %i de rejeição financeira', async status => {
    vi.mocked(api.submit).mockRejectedValue(new ApiError('Não enviada', status));
    await workflow.submit(request);
    expect(workflow.getSnapshot().phase).toBe('sendFailed');
    expect(store.load()).toEqual(request);
  });
  it('respeita Retry-After inclusive no botão de retry', async () => {
    vi.mocked(api.submit).mockRejectedValue(new ApiError('Limite', 429, 'RATE_LIMIT', 3000));
    await workflow.submit(request); await workflow.retry();
    await vi.advanceTimersByTimeAsync(2999);
    expect(api.get).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(api.get).toHaveBeenCalledTimes(1);
  });
  it('recusa confirmação com ID ou payload divergente', async () => {
    vi.mocked(api.submit).mockResolvedValue({ ...accepted, quantity: 1 });
    await workflow.submit(request);
    expect(workflow.getSnapshot().phase).toBe('sendFailed');
    expect(store.load()).toEqual(request);
  });
  it('não envia se não conseguir persistir antes do POST', async () => {
    workflow.stop();
    workflow = new OrderWorkflow(api, { load: () => null, save: () => { throw new Error('Quota'); }, clear: () => {} }); workflow.start();
    await workflow.submit(request);
    expect(api.submit).not.toHaveBeenCalled();
    expect(workflow.getSnapshot().storageError).toBeTruthy();
  });
  it('interrompe timers e aborta requisições ao sair', async () => {
    await workflow.submit(request);
    const signal = vi.mocked(api.submit).mock.calls[0][1];
    workflow.stop();
    expect(signal?.aborted).toBe(true);
    await vi.advanceTimersByTimeAsync(10000);
    expect(api.get).not.toHaveBeenCalled();
    expect(store.load()).toEqual(request);
  });
  it('não aceita estado definitivo sem ExecutionReport', async () => {
    vi.mocked(api.submit).mockResolvedValue({ ...pending, status: 'Accepted' });
    await workflow.submit(request);
    expect(workflow.getSnapshot().phase).not.toBe('accepted');
    expect(store.load()).toEqual(request);
  });
});
