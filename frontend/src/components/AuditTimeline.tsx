import type { OrderApi } from "../api/types";
import { usePolling } from "../state/hooks";
import { dateTime } from "./ResultPanel";
export function AuditTimeline({
  api,
  clOrdId,
}: {
  api: OrderApi;
  clOrdId: string;
}) {
  const { data, error, loading } = usePolling(
    (signal) => api.events(clOrdId, signal),
    `events:${clOrdId}`,
  );
  const events = [
    ...new Map(
      (data?.events || [])
        .filter((e) => e.clOrdId === clOrdId)
        .map((e) => [e.eventId, e]),
    ).values(),
  ].sort((a, b) => a.occurredAt.localeCompare(b.occurredAt));
  return (
    <section
      id="audit-panel"
      tabIndex={-1}
      className="card audit-card"
      aria-labelledby="audit-title"
    >
      <div className="section-title">
        <div>
          <span className="eyebrow">05 / HISTÓRICO</span>
          <h2 id="audit-title">Linha do tempo</h2>
        </div>
        <span className="pill purple">AUDITORIA</span>
      </div>
      <p className="muted">
        Histórico da ordem selecionada: registro, tentativas de envio,
        confirmação FIX e auditoria Kafka. A projeção Kafka pode chegar depois e
        não altera a decisão FIX.
      </p>
      <p className="mono audit-id">{clOrdId}</p>
      <div role="status" aria-live="polite">
        <span className="sr-only">Auditoria: </span>
        {error || data?.available === false
          ? "Auditoria temporariamente indisponível. A decisão permanece válida."
          : loading
            ? "Consultando auditoria…"
            : !events.length
              ? "Aguardando os eventos de auditoria."
              : `${events.length} evento(s) recebido(s).`}
      </div>
      {data?.kafkaAvailable === false && (
        <p className="notice">
          Kafka indisponível: exibindo o histórico já persistido. Novos eventos
          de auditoria podem chegar depois.
        </p>
      )}
      <ol className="timeline">
        {events.map((event) => (
          <li key={event.eventId}>
            <time dateTime={event.occurredAt}>
              {dateTime(event.occurredAt)}
            </time>
            <small>
              {event.source ?? "Kafka"} · {event.type}
            </small>
            <p>{event.description}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}
