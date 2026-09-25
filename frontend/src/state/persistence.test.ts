import { expect, it } from 'vitest';
import { createPendingStore } from './persistence';
import { request } from '../test/fixtures';

it.each([['Buy', 'B'], ['Sell', 'S']])('recupera pendência %s preservando o mesmo ID', (stored, code) => {
    const key = 'orderflow:test:pending:v1';
    localStorage.setItem(key, JSON.stringify({ version: 1, request: { ...request, side: stored } }));
    const recovered = createPendingStore(localStorage, 'test').load();
    expect(recovered).toEqual({ ...request, side: code });
    expect(JSON.parse(localStorage.getItem(key)!).request).toEqual(recovered);
});
