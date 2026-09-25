import { useEffect, useRef, useState, type FormEvent } from "react";
import { symbols, type OrderRequest } from "../domain/order";
import {
  money,
  priceInCents,
  quantityValue,
  toRequest,
  validate,
  type Draft,
  type Errors,
} from "../domain/validation";
export function OrderForm({
  blocked,
  submit,
  serverErrors,
}: {
  serverErrors?: Record<string, string[]>;
  blocked: boolean;
  submit: (request: OrderRequest) => Promise<void>;
}) {
  const [draft, setDraft] = useState<Draft>({
    symbol: "PETR4",
    side: "B",
    quantity: "",
    price: "",
  });
  const [errors, setErrors] = useState<Errors>({});
  const [message, setMessage] = useState("");
  const form = useRef<HTMLFormElement>(null);
  useEffect(() => {
    if (!serverErrors) return;
    const fields: (keyof Draft)[] = ["symbol", "side", "quantity", "price"];
    const next: Errors = {};
    for (const field of fields)
      if (serverErrors[field]?.length)
        next[field] = serverErrors[field].join(" ");
    setErrors(next);
    const first = fields.find((field) => next[field]);
    if (first)
      form.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus();
  }, [serverErrors]);
  const quantity = quantityValue(draft.quantity);
  const price = priceInCents(draft.price);
  const change = (field: keyof Draft, value: string) => {
    setDraft((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => ({ ...previous, [field]: undefined }));
  };
  function blur(field: keyof Draft) {
    setErrors((previous) => ({ ...previous, [field]: validate(draft)[field] }));
  }
  function send(event: FormEvent) {
    event.preventDefault();
    if (blocked) return;
    const next = validate(draft);
    setErrors(next);
    const first = Object.keys(next)[0];
    if (first) {
      form.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus();
      return;
    }
    if (!globalThis.crypto?.randomUUID) {
      setMessage(
        "Este navegador precisa de HTTPS ou localhost para gerar um ID seguro.",
      );
      return;
    }
    setMessage("");
    void submit(toRequest(draft, crypto.randomUUID())).catch(() =>
      setMessage(
        "Não foi possível enviar a solicitação. Consulte o resultado antes de tentar novamente.",
      ),
    );
  }
  return (
    <section className="card form-card" aria-labelledby="new-order-title">
      <div className="section-title">
        <div>
          <span className="eyebrow">01 / NEGOCIAR</span>
          <h2 id="new-order-title">Nova ordem</h2>
        </div>
        <span className="pill">LIMITADA</span>
      </div>
      {Object.values(errors).some(Boolean) && (
        <p role="alert" className="notice">
          Confira os campos destacados antes de enviar.
        </p>
      )}
      <form ref={form} onSubmit={send} noValidate>
        <fieldset disabled={blocked}>
          <legend className="sr-only">Dados da nova ordem</legend>
          <label htmlFor="symbol">Símbolo</label>
          <select
            id="symbol"
            name="symbol"
            value={draft.symbol}
            onChange={(e) => change("symbol", e.target.value)}
            onBlur={() => blur("symbol")}
            aria-invalid={!!errors.symbol}
            aria-describedby={errors.symbol ? "symbol-error" : undefined}
          >
            {symbols.map((symbol) => (
              <option key={symbol}>{symbol}</option>
            ))}
          </select>
          {errors.symbol && (
            <p className="field-error" id="symbol-error">
              {errors.symbol}
            </p>
          )}
          <label htmlFor="side">Lado da operação</label>
          <select
            id="side"
            name="side"
            value={draft.side}
            onChange={(e) => change("side", e.target.value)}
            onBlur={() => blur("side")}
            aria-invalid={!!errors.side}
            aria-describedby={errors.side ? "side-error" : undefined}
          >
            <option value="B">Compra</option>
            <option value="S">Venda</option>
          </select>
          {errors.side && (
            <p className="field-error" id="side-error">
              {errors.side}
            </p>
          )}
          <div className="field-grid">
            <div>
              <label htmlFor="quantity">Quantidade</label>
              <input
                id="quantity"
                name="quantity"
                inputMode="numeric"
                autoComplete="off"
                placeholder="100"
                value={draft.quantity}
                onChange={(e) => change("quantity", e.target.value)}
                onBlur={() => blur("quantity")}
                aria-invalid={!!errors.quantity}
                aria-describedby="quantity-hint quantity-error"
              />
              <small id="quantity-hint">De 1 a 99.999 unidades</small>
              <p className="field-error" id="quantity-error">
                {errors.quantity}
              </p>
            </div>
            <div>
              <label htmlFor="price">Preço unitário (R$)</label>
              <input
                id="price"
                name="price"
                inputMode="decimal"
                autoComplete="off"
                placeholder="35,50"
                value={draft.price}
                onChange={(e) => change("price", e.target.value)}
                onBlur={() => blur("price")}
                aria-invalid={!!errors.price}
                aria-describedby="price-hint price-error"
              />
              <small id="price-hint">De R$ 0,01 a R$ 999,99</small>
              <p className="field-error" id="price-error">
                {errors.price}
              </p>
            </div>
          </div>
          <div className="notional">
            <span>
              Valor total da ordem<small>Preço × quantidade</small>
            </span>
            <output aria-label="Valor total da ordem">
              {quantity && price ? money(quantity * price) : "R$ —"}
            </output>
          </div>
          <button className="primary" type="submit">
            Enviar ordem <span aria-hidden="true">↗</span>
          </button>
        </fieldset>
        <p className="form-note">
          {blocked
            ? "Conclua a consulta da solicitação atual antes de enviar outra ordem."
            : "A confirmação será exibida após a análise da ordem."}
        </p>
        {message && (
          <p role="alert" className="field-error">
            {message}
          </p>
        )}
      </form>
    </section>
  );
}
