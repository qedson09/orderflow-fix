import type { Snapshot } from "../state/workflow";
import { isFinal } from "../domain/order";
import { money, priceInCents } from "../domain/validation";
export const labels = {
  validationFailed: "Confira os dados da ordem",
  idle: "Aguardando nova ordem",
  sending: "Enviando solicitação",
  pending: "Ordem pendente",
  unknown: "Resultado desconhecido",
  accepted: "Ordem aceita",
  rejected: "Ordem rejeitada",
  sendFailed: "Falha ao enviar",
  fixUnavailable: "Sessão FIX indisponível",
};
export function dateTime(value: string) {
  return new Date(value).toLocaleString("pt-BR");
}
export function ResultPanel({
  state,
  retry,
}: {
  state: Snapshot;
  retry: () => Promise<void>;
}) {
  const request = state.request;
  const terminal = isFinal(state.order);
  return (
    <section
      className={`card result-card result-${state.phase}`}
      aria-labelledby="result-title"
    >
      <span className="eyebrow">02 / ACOMPANHAR</span>
      <h2 id="result-title">Resultado da solicitação</h2>
      <div
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="result-status"
      >
        <span className="status-icon" aria-hidden="true">
          {state.phase === "accepted"
            ? "✓"
            : state.phase === "rejected"
              ? "×"
              : "◷"}
        </span>
        <div>
          <h3>{labels[state.phase]}</h3>
          <p>
            {state.phase === "validationFailed"
              ? "A solicitação não foi aceita pela API. Corrija os campos e envie novamente."
              : terminal
                ? "Decisão confirmada pelo ExecutionReport FIX."
                : request
                  ? "Nenhuma decisão definitiva foi confirmada."
                  : "Envie uma ordem para acompanhar a decisão aqui."}
          </p>
        </div>
      </div>
      {state.message && (
        <p role="alert" className="notice">
          {state.message}
        </p>
      )}
      {state.storageError && (
        <p role="alert" className="notice">
          {state.storageError}
        </p>
      )}
      {request ? (
        <>
          <dl className="details">
            <div className="wide">
              <dt>ClOrdID</dt>
              <dd className="mono">{request.clOrdId}</dd>
            </div>
            <div className="wide">
              <dt>Cliente / conta da solicitação</dt>
              <dd>{request.accountId ?? "CLIENTE-001"}</dd>
            </div>
            <div>
              <dt>Símbolo</dt>
              <dd>{request.symbol}</dd>
            </div>
            <div>
              <dt>Lado</dt>
              <dd>{request.side === "B" ? "Compra" : "Venda"}</dd>
            </div>
            <div>
              <dt>Quantidade</dt>
              <dd>{request.quantity.toLocaleString("pt-BR")}</dd>
            </div>
            <div>
              <dt>Preço</dt>
              <dd>{money(priceInCents(request.price)!)}</dd>
            </div>
            <div>
              <dt>Valor total</dt>
              <dd>{money(request.quantity * priceInCents(request.price)!)}</dd>
            </div>
            <div>
              <dt>Última atualização</dt>
              <dd>
                {state.order
                  ? dateTime(state.order.updatedAt)
                  : "Aguardando a API"}
              </dd>
            </div>
          </dl>
          {state.order?.rejectionReason && (
            <p className="notice">
              <strong>Motivo da rejeição:</strong> {state.order.rejectionReason}
            </p>
          )}
          {!terminal && state.phase !== "validationFailed" && (
            <button
              className="secondary"
              onClick={() => void retry()}
              disabled={state.busy}
            >
              Tentar novamente com o mesmo ID
            </button>
          )}
          {!terminal && state.phase !== "validationFailed" && (
            <p className="form-note">
              Consulta automática em andamento. Reenviar preserva os dados e o
              ID original.
            </p>
          )}
        </>
      ) : (
        <div className="empty-result">
          <div className="flow-glyph" aria-hidden="true">
            ↗
          </div>
          <p>
            Uma ordem.
            <br />
            Um ID do início ao fim.
          </p>
        </div>
      )}
    </section>
  );
}
