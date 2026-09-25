import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { App } from "../App";
import { createPendingStore } from "../state/persistence";
import { accepted, fakeApi, request } from "../test/fixtures";
import { exposureMoney, ExposurePanel } from "./ExposurePanel";

it("formata exposição negativa sem arredondamento de centavos", () => {
  expect(exposureMoney("-24975000.01")).toBe("−R$ 24.975.000,01");
});
it("mostra estado vazio sem listar ativos não negociados", async () => {
  render(
    <ExposurePanel api={fakeApi()} accountId="CLIENTE-001" refreshKey="idle" />,
  );
  expect(
    await screen.findByText(
      "Este cliente ainda não possui ativos com ordens aceitas.",
    ),
  ).toBeInTheDocument();
  expect(screen.queryByRole("table")).not.toBeInTheDocument();
});
it("não apresenta quadro de conexões e mantém os cinco quadros na ordem solicitada", () => {
  render(
    <App
      api={fakeApi()}
      store={createPendingStore(localStorage, "layout-test")}
    />,
  );
  expect(
    screen
      .getAllByRole("heading", { level: 2 })
      .map((element) => element.textContent),
  ).toEqual([
    "Nova ordem",
    "Resultado da solicitação",
    "Exposição do cliente",
    "Ordens recentes",
    "Linha do tempo",
  ]);
  expect(screen.queryByText(/CONEXÕES/)).not.toBeInTheDocument();
});
it("mostra limite e capacidades distintas por lado e troca o cliente consultado", async () => {
  const api = fakeApi();
  vi.mocked(api.exposures).mockResolvedValue({
    accountId: "CLIENTE-001",
    checkedAt: accepted.createdAt,
    items: [
      {
        accountId: "CLIENTE-001",
        symbol: "PETR4",
        currentExposure: "-3500.00",
        absoluteLimit: "100000000.00",
        availableForBuy: "100003500.00",
        availableForSell: "99996500.00",
        utilizationPercentage: 0.0035,
        version: 1,
      },
    ],
  });
  render(
    <App api={api} store={createPendingStore(localStorage, "exposure-test")} />,
  );
  await screen.findByText("−R$ 3.500,00");
  expect(screen.getByText("R$ 100.003.500,00")).toBeInTheDocument();
  expect(screen.getByText("R$ 99.996.500,00")).toBeInTheDocument();
  fireEvent.change(screen.getByLabelText("Cliente / conta de demonstração"), {
    target: { value: "CLIENTE-002" },
  });
  await waitFor(() =>
    expect(api.exposures).toHaveBeenLastCalledWith(
      "CLIENTE-002",
      expect.any(AbortSignal),
    ),
  );
  await waitFor(() =>
    expect(api.list).toHaveBeenLastCalledWith(
      expect.any(AbortSignal),
      "CLIENTE-002",
      1,
      10,
    ),
  );
});
it("não apresenta zero como saldo confirmado quando a consulta falha", async () => {
  const api = fakeApi();
  vi.mocked(api.exposures).mockRejectedValue(new Error("offline"));
  render(<ExposurePanel api={api} accountId="CLIENTE-001" refreshKey="idle" />);
  expect(
    await screen.findByText(/Nenhum saldo disponível foi confirmado/),
  ).toBeInTheDocument();
});
it("Ver auditoria abre todos os eventos da ordem selecionada e elimina duplicatas", async () => {
  const api = fakeApi();
  vi.mocked(api.list).mockResolvedValue({
    items: [accepted],
    page: 1,
    pageSize: 10,
    totalCount: 1,
    totalPages: 1,
  });
  const events = [
    "Registro",
    "Tentativa de envio",
    "FIX confirmado",
    "Auditoria Kafka",
  ].map((description, index) => ({
    eventId: crypto.randomUUID(),
    clOrdId: request.clOrdId,
    description,
    type: `stage-${index}`,
    occurredAt: accepted.createdAt,
  }));
  vi.mocked(api.events).mockResolvedValue({
    available: true,
    events: [...events, events[0]],
  });
  render(
    <App api={api} store={createPendingStore(localStorage, "audit-test")} />,
  );
  fireEvent.click(
    await screen.findByRole("button", {
      name: `Ver auditoria da ordem ${request.clOrdId}`,
    }),
  );
  await screen.findByText("4 evento(s) recebido(s).");
  expect(api.events).toHaveBeenCalledWith(
    request.clOrdId,
    expect.any(AbortSignal),
  );
  const panel = screen.getByRole("region", { name: "Linha do tempo" });
  expect(within(panel).getAllByRole("listitem")).toHaveLength(4);
  for (const event of events)
    expect(within(panel).getByText(event.description)).toBeInTheDocument();
});
