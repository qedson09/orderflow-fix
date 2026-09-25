import type { OrderApi } from "../api/types";
import { usePolling } from "../state/hooks";
import { dateTime } from "./ResultPanel";

// Currency strings are converted to integer cents, including negative exposure.
export function exposureMoney(value: string) {
  const negative = value.startsWith("-");
  const [whole, fraction] = value.replace("-", "").split(".");
  const cents = BigInt(whole) * 100n + BigInt(fraction);
  const units = (cents / 100n).toLocaleString("pt-BR");
  return `${negative ? "−" : ""}R$ ${units},${(cents % 100n).toString().padStart(2, "0")}`;
}
export function ExposurePanel({
  api,
  accountId,
  refreshKey,
}: {
  api: OrderApi;
  accountId: string;
  refreshKey: string;
}) {
  const { data, error, loading } = usePolling(
    (signal) => api.exposures(accountId, signal),
    `exposure:${accountId}:${refreshKey}`,
  );
  return (
    <section className="card exposure-card" aria-labelledby="exposure-title">
      <div className="section-title">
        <div>
          <span className="eyebrow">03 / EXPOSIÇÃO</span>
          <h2 id="exposure-title">Exposição do cliente</h2>
        </div>
        <span className="pill">{accountId}</span>
      </div>
      <p className="muted">
        Somente ativos com ordens aceitas deste cliente, inclusive posições
        zeradas após compra e venda. Exposição comprometida, não saldo em
        dinheiro nem execução em mercado.
      </p>
      {loading && <p role="status">Consultando exposição…</p>}
      {error && (
        <p role="status" className="notice">
          Não foi possível atualizar a exposição.{" "}
          {data
            ? "Os valores abaixo podem estar desatualizados."
            : "Nenhum saldo disponível foi confirmado."}
        </p>
      )}
      {data && !error && data.items.length === 0 && (
        <p role="status" className="muted">
          Este cliente ainda não possui ativos com ordens aceitas.
        </p>
      )}
      {data && data.items.length > 0 && (
        <>
          <div
            className="table-scroll"
            role="region"
            aria-label="Exposição por ativo"
            tabIndex={0}
          >
            <table>
              <thead>
                <tr>
                  <th>Ativo</th>
                  <th>Exposição</th>
                  <th>Limite absoluto</th>
                  <th>Utilização</th>
                  <th>Disponível compra</th>
                  <th>Disponível venda</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((item) => (
                  <tr key={item.symbol}>
                    <td>
                      <strong>{item.symbol}</strong>
                    </td>
                    <td>{exposureMoney(item.currentExposure)}</td>
                    <td>{exposureMoney(item.absoluteLimit)}</td>
                    <td>
                      {item.utilizationPercentage.toLocaleString("pt-BR", {
                        maximumFractionDigits: 2,
                      })}
                      %
                    </td>
                    <td>{exposureMoney(item.availableForBuy)}</td>
                    <td>{exposureMoney(item.availableForSell)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted">
            Consulta: {dateTime(data.checkedAt)}. Valores informativos; cada
            nova ordem passa pela validação de limite no servidor.
          </p>
        </>
      )}
    </section>
  );
}
