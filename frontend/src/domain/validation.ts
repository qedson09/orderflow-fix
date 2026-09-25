import { symbols, type OrderRequest } from './order';
export type Draft = { symbol: string; side: string; quantity: string; price: string };
export type Errors = Partial<Record<keyof Draft, string>>;

export function priceInCents(input: string): number | null {
    // Não aceita milhares, notação científica, sinais, espaços ou arredondamento.
    if (!/^\d{1,3}(?:[.,]\d{1,2})?$/.test(input)) return null;
    const [whole, fraction = ''] = input.split(/[.,]/);
    const cents = Number(whole) * 100 + Number(fraction.padEnd(2, '0'));
    return cents >= 1 && cents <= 99999 ? cents : null;
}
export function quantityValue(input: string): number | null {
    if (!/^\d{1,5}$/.test(input)) return null;
    const value = Number(input);
    return value >= 1 && value <= 99999 ? value : null;
}
export function invariantPrice(cents: number) {
    return `${Math.floor(cents / 100)}.${String(cents % 100).padStart(2, '0')}`;
}
export function money(cents: number) {
    return `R$ ${Math.floor(cents / 100).toLocaleString('pt-BR')},${String(cents % 100).padStart(2, '0')}`;
}
export function validate(draft: Draft): Errors {
    const errors: Errors = {};
    if (!symbols.includes(draft.symbol as typeof symbols[number])) errors.symbol = 'Selecione PETR4, VALE3 ou VIIA4.';
    if (!['B', 'S'].includes(draft.side)) errors.side = 'Escolha Compra ou Venda.';
    if (quantityValue(draft.quantity) === null) errors.quantity = draft.quantity.trim() === '' ? 'Informe a quantidade da ordem.' : 'Informe uma quantidade inteira entre 1 e 99.999, sem letras ou casas decimais.';
    if (priceInCents(draft.price) === null) errors.price = draft.price.trim() === '' ? 'Informe o preço unitário da ordem.' : 'Informe de R$ 0,01 a R$ 999,99, com até duas casas decimais.';
    return errors;
}
export function toRequest(draft: Draft, clOrdId: string): OrderRequest {
    if (Object.keys(validate(draft)).length) throw new Error('Formulário inválido');
    return {
        clOrdId, symbol: draft.symbol as OrderRequest['symbol'], side: draft.side as OrderRequest['side'],
        quantity: quantityValue(draft.quantity)!, price: invariantPrice(priceInCents(draft.price)!)
    };
}
