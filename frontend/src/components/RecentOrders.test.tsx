import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { RecentOrders } from "./RecentOrders";
import { accepted, fakeApi } from "../test/fixtures";

it("navega por páginas, altera tamanho e seleciona a auditoria do registro", async () => {
  const api = fakeApi();
  vi.mocked(api.list).mockImplementation(
    async (_signal, _account, page = 1, pageSize = 10) => ({
      items: [
        {
          ...accepted,
          clOrdId:
            page === 1
              ? accepted.clOrdId
              : "fb45b124-4daf-4cf4-9589-248464ca1403",
        },
      ],
      page,
      pageSize,
      totalCount: 21,
      totalPages: Math.ceil(21 / pageSize),
    }),
  );
  const selectAudit = vi.fn();
  render(
    <RecentOrders
      api={api}
      accountId="CLIENTE-001"
      refreshKey=""
      selectAudit={selectAudit}
    />,
  );
  await screen.findByText("Página 1 de 3 · 21 ordens");
  expect(screen.getByRole("button", { name: "Anterior" })).toBeDisabled();
  fireEvent.click(screen.getByRole("button", { name: "Próxima" }));
  await screen.findByText("Página 2 de 3 · 21 ordens");
  expect(api.list).toHaveBeenLastCalledWith(
    expect.any(AbortSignal),
    "CLIENTE-001",
    2,
    10,
  );
  fireEvent.click(
    screen.getByRole("button", { name: /Ver auditoria da ordem/ }),
  );
  expect(selectAudit).toHaveBeenCalledWith(
    "fb45b124-4daf-4cf4-9589-248464ca1403",
  );
  fireEvent.click(screen.getByRole("button", { name: "Anterior" }));
  await screen.findByText("Página 1 de 3 · 21 ordens");
  fireEvent.change(screen.getByLabelText("Ordens por página"), {
    target: { value: "50" },
  });
  await screen.findByText("Página 1 de 1 · 21 ordens");
  expect(api.list).toHaveBeenLastCalledWith(
    expect.any(AbortSignal),
    "CLIENTE-001",
    1,
    50,
  );
  expect(screen.getByRole("button", { name: "Próxima" })).toBeDisabled();
});

it("bloqueia navegação quando não há registros", async () => {
  render(
    <RecentOrders
      api={fakeApi()}
      accountId="CLIENTE-001"
      refreshKey=""
      selectAudit={vi.fn()}
    />,
  );
  await screen.findByText("Nenhuma ordem encontrada");
  expect(screen.getByRole("button", { name: "Anterior" })).toBeDisabled();
  expect(screen.getByRole("button", { name: "Próxima" })).toBeDisabled();
});

it("aborta a consulta quando o quadro é desmontado", async () => {
  const api = fakeApi();
  const view = render(
    <RecentOrders
      api={api}
      accountId="CLIENTE-001"
      refreshKey=""
      selectAudit={vi.fn()}
    />,
  );
  await waitFor(() => expect(api.list).toHaveBeenCalled());
  const signal = vi.mocked(api.list).mock.calls[0][0]!;
  view.unmount();
  expect(signal.aborted).toBe(true);
});
