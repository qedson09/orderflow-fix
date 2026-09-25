import { useEffect, useState } from "react";
import type { OrderApi } from "../api/types";
import { usePolling } from "../state/hooks";
import { money, priceInCents } from "../domain/validation";
import { dateTime } from "./ResultPanel";
export function RecentOrders({
  api,
  accountId,
  refreshKey,
  selectAudit,
}: {
  api: OrderApi;
  accountId: string;
  refreshKey: string;
  selectAudit: (id: string) => void;
}) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const { data, error, loading } = usePolling(
    (signal) => api.list(signal, accountId, page, pageSize),
    `recent:${accountId}:${refreshKey}:${page}:${pageSize}`,
  );
  useEffect(() => {
    if (data && data.page !== page) setPage(data.page);
  }, [data, page]);
  const labels = {
    Pending: "Pendente",
    Accepted: "Aceita",
    Rejected: "Rejeitada",
  };
  return (
    <section className="card recent-card" aria-labelledby="recent-title">
      <div className="section-title">
        <div>
          <span className="eyebrow">04 / CONSULTAR</span>
          <h2 id="recent-title">Ordens recentes</h2>
        </div>
        <span className="count">{data?.totalCount ?? 0}</span>
      </div>
      {error && (
        <p role="status" className="notice">
          {error} A lista pode estar desatualizada.
        </p>
      )}
      {loading ? (
        <p className="muted">Consultando ordens…</p>
      ) : !data?.items.length ? (
        <p className="muted empty-list">
          Suas ordens aparecerão aqui após o primeiro envio.
        </p>
      ) : (
        <div
          className="table-scroll"
          role="region"
          aria-label="Tabela de ordens recentes"
          tabIndex={0}
        >
          <table>
            <thead>
              <tr>
                <th>Símbolo / ID</th>
                <th>Lado</th>
                <th>Qtd.</th>
                <th>Preço</th>
                <th>Estado</th>
                <th>Atualização</th>
                <th>
                  <span className="sr-only">Ações</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((order) => (
                <tr key={order.clOrdId}>
                  <td>
                    <strong>{order.symbol}</strong>
                    <small className="mono" title={order.clOrdId}>
                      {order.clOrdId.slice(0, 8)}…
                    </small>
                  </td>
                  <td>{order.side === "B" ? "Compra" : "Venda"}</td>
                  <td>{order.quantity.toLocaleString("pt-BR")}</td>
                  <td>{money(priceInCents(order.price)!)}</td>
                  <td>
                    <span className={`badge ${order.status.toLowerCase()}`}>
                      {labels[order.status]}
                    </span>
                  </td>
                  <td>{dateTime(order.updatedAt)}</td>
                  <td>
                    <button
                      className="text-button"
                      aria-controls="audit-panel"
                      onClick={() => selectAudit(order.clOrdId)}
                      aria-label={`Ver auditoria da ordem ${order.clOrdId}`}
                    >
                      Ver auditoria ↓
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <nav className="pagination" aria-label="Paginação das ordens">
        <label htmlFor="orders-page-size">
          Ordens por página
          <select
            id="orders-page-size"
            value={pageSize}
            onChange={(event) => {
              setPageSize(Number(event.target.value));
              setPage(1);
            }}
          >
            <option value="10">10</option>
            <option value="20">20</option>
            <option value="50">50</option>
          </select>
        </label>
        <span role="status">
          {data
            ? data.totalCount === 0
              ? "Nenhuma ordem encontrada"
              : `Página ${data.page} de ${data.totalPages} · ${data.totalCount} ordens`
            : "Carregando página…"}
        </span>
        <div className="pagination-actions">
          <button
            type="button"
            disabled={loading || !data || data.page <= 1}
            onClick={() => setPage(page - 1)}
          >
            Anterior
          </button>
          <button
            type="button"
            disabled={loading || !data || data.page >= data.totalPages}
            onClick={() => setPage(page + 1)}
          >
            Próxima
          </button>
        </div>
      </nav>
    </section>
  );
}
