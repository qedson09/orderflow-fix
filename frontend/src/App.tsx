import { useEffect, useState, useSyncExternalStore } from "react";
import type { OrderApi } from "./api/types";
import type { PendingStore } from "./state/persistence";
import { OrderWorkflow } from "./state/workflow";
import { accounts, isFinal } from "./domain/order";
import { OrderForm } from "./components/OrderForm";
import { ResultPanel } from "./components/ResultPanel";
import { RecentOrders } from "./components/RecentOrders";
import { AuditTimeline } from "./components/AuditTimeline";
import { ExposurePanel } from "./components/ExposurePanel";

export function App({
  api,
  store,
  simulated = false,
}: {
  api: OrderApi;
  store: PendingStore;
  simulated?: boolean;
}) {
  const [workflow] = useState(() => new OrderWorkflow(api, store));
  const state = useSyncExternalStore(workflow.subscribe, workflow.getSnapshot);
  const [auditId, setAuditId] = useState<string>();
  const [accountId, setAccountId] = useState<string>(
    () => state.request?.accountId ?? "CLIENTE-001",
  );
  useEffect(() => {
    workflow.start();
    return workflow.stop;
  }, [workflow]);
  const blocked =
    !!state.storageError ||
    (!!state.request &&
      state.phase !== "validationFailed" &&
      !isFinal(state.order));
  const selectedId =
    auditId ||
    (state.phase !== "validationFailed" &&
    (state.request?.accountId ?? "CLIENTE-001") === accountId
      ? state.request?.clOrdId
      : undefined);
  useEffect(() => {
    if (auditId) {
      const panel = document.getElementById("audit-panel");
      panel?.scrollIntoView?.({ behavior: "smooth", block: "start" });
      panel?.focus();
    }
  }, [auditId]);
  return (
    <>
      <a className="skip-link" href="#main">
        Ir para o conteúdo
      </a>
      <header className="topbar">
        <a className="brand" href="#main">
          <span className="brand-icon" aria-hidden="true">
            ↗
          </span>
          OrderFlow<span className="brand-dot">.</span>
        </a>
        <span className="header-caption">PAINEL DE ORDENS</span>
        <span className="header-tag">FIX 4.4</span>
      </header>
      <main id="main">
        {simulated && (
          <p className="demo-banner" role="status">
            <strong>Modo de demonstração.</strong> Todos os dados e conexões são
            simulados. VIIA4 demonstra uma rejeição.
          </p>
        )}
        <div className="hero">
          <div>
            <span className="eyebrow">CONTROLE EM CADA OPERAÇÃO</span>
            <h1>
              Suas ordens.
              <br />
              <span>Em um só fluxo.</span>
            </h1>
            <p>Envie, acompanhe e consulte cada decisão.</p>
          </div>
        </div>
        <div className="account-selector">
          <label htmlFor="account">Cliente / conta de demonstração</label>
          <select
            id="account"
            value={accountId}
            disabled={blocked}
            onChange={(e) => {
              setAccountId(e.target.value);
              setAuditId(undefined);
            }}
          >
            {accounts.map((id) => (
              <option key={id} value={id}>
                {id}
              </option>
            ))}
          </select>
          <small>Seleção demonstrativa, sem autenticação.</small>
        </div>
        <div className="workspace">
          <OrderForm
            blocked={blocked}
            serverErrors={state.fieldErrors}
            submit={(request) => {
              setAuditId(undefined);
              return workflow.submit({
                ...request,
                accountId: accountId as (typeof accounts)[number],
              });
            }}
          />
          <ResultPanel state={state} retry={workflow.retry} />
        </div>
        <ExposurePanel
          api={api}
          accountId={accountId}
          refreshKey={state.phase}
        />
        <RecentOrders
          key={accountId}
          api={api}
          accountId={accountId}
          refreshKey={`${state.request?.clOrdId || ""}:${state.phase}`}
          selectAudit={(id) => {
            setAuditId(id);
            document
              .getElementById("audit-panel")
              ?.scrollIntoView?.({ behavior: "smooth" });
          }}
        />
        {selectedId ? (
          <AuditTimeline key={selectedId} api={api} clOrdId={selectedId} />
        ) : (
          <section id="audit-panel" className="card" tabIndex={-1}>
            <span className="eyebrow">05 / HISTÓRICO</span>
            <h2>Linha do tempo</h2>
            <p className="muted">
              Envie uma ordem ou selecione Ver auditoria na lista para consultar
              todos os eventos daquela ordem.
            </p>
          </section>
        )}
      </main>
      <footer>
        <span>OrderFlow · orderflow-fix</span>
        <span>
          Aceite representa exposição comprometida, sem execução de mercado.
        </span>
      </footer>
    </>
  );
}
