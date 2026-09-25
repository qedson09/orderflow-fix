import { expect, it } from 'vitest';
import { createMockApi } from './mock';
import { accepted } from '../test/fixtures';

it.each([['Buy', 'B'], ['Sell', 'S']])('recupera lado %s na demonstração com código de um caractere', async (side, code) => {
    const key = 'orderflow:mock:orders:v1';
    localStorage.setItem(key, JSON.stringify([{ order: { ...accepted, side }, eventId: crypto.randomUUID() }]));
    const api = createMockApi(localStorage);
    expect((await api.list(undefined, 'CLIENTE-001', 1, 10)).items[0].side).toBe(code);
    expect(JSON.parse(localStorage.getItem(key)!)[0].order.side).toBe(code);
});
