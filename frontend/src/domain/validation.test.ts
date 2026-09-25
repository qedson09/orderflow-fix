import { describe, expect, it } from 'vitest';
import { invariantPrice, money, priceInCents, quantityValue, toRequest } from './validation';
import { request } from '../test/fixtures';
describe('quantidade', () => {
    it.each(['0', '-1', '100000', '1.5', '1,5', '1e3', '', ' 1', '+1'])('recusa %s', value => expect(quantityValue(value)).toBeNull());
    it.each([['1', 1], ['99999', 99999], ['100', 100]])('aceita %s', (value, expected) => expect(quantityValue(value as string)).toBe(expected));
});
describe('preço em centavos', () => {
    it.each(['0', '0.00', '-0.01', '1000', '1.001', '1,001', '1e2', '1.000,00', '', 'NaN', ' 1', '.50'])('recusa %s', value => expect(priceInCents(value)).toBeNull());
    it.each([['0,01', 1], ['999.99', 99999], ['35,5', 3550], ['35.50', 3550], ['1', 100]])('aceita %s', (value, expected) => expect(priceInCents(value as string)).toBe(expected));
    it('mantém centavos no maior valor total, sem ponto flutuante financeiro', () => {
        expect(99999 * priceInCents('999.99')!).toBe(9999800001);
        expect(money(9999800001)).toBe('R$ 99.998.000,01');
        expect(invariantPrice(3550)).toBe('35.50');
        expect(toRequest({ symbol: 'PETR4', side: 'B', quantity: '100', price: '35,50' }, request.clOrdId)).toEqual(request);
    });
});
