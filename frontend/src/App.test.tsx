import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { App } from "./App";
import { createPendingStore } from "./state/persistence";
import { accepted, fakeApi, request } from "./test/fixtures";
afterEach(() => vi.useRealTimers());
it("associa erros aos campos e foca o primeiro inválido", () => {
  render(
    <App api={fakeApi()} store={createPendingStore(localStorage, "test")} />,
  );
  fireEvent.click(screen.getByRole("button", { name: /Enviar ordem/ }));
  expect(screen.getByLabelText("Quantidade")).toHaveAttribute(
    "aria-invalid",
    "true",
  );
  expect(screen.getByLabelText("Quantidade")).toHaveFocus();
  expect(
    screen.getByLabelText("Preço unitário (R$)"),
  ).toHaveAccessibleDescription(
    expect.stringContaining("Informe o preço unitário"),
  );
});
it("preenche, calcula total e envia preço em centavos com UUID", async () => {
  const api = fakeApi();
  vi.mocked(api.submit).mockImplementation(async (value) => ({
    ...accepted,
    ...value,
  }));
  render(<App api={api} store={createPendingStore(localStorage, "test")} />);
  fireEvent.change(screen.getByLabelText("Quantidade"), {
    target: { value: "100" },
  });
  fireEvent.change(screen.getByLabelText("Preço unitário (R$)"), {
    target: { value: "35,50" },
  });
  expect(screen.getByLabelText("Valor total da ordem")).toHaveTextContent(
    "R$ 3.550,00",
  );
  fireEvent.click(screen.getByRole("button", { name: /Enviar ordem/ }));
  await waitFor(() =>
    expect(screen.getByText("Ordem aceita")).toBeInTheDocument(),
  );
  expect(api.submit).toHaveBeenCalledWith(
    expect.objectContaining({
      price: "35.50",
      quantity: 100,
      clOrdId: expect.stringMatching(/^[0-9a-f-]{36}$/),
    }),
    expect.any(AbortSignal),
  );
});
it("eventos tardios ou indisponíveis não alteram aceite confirmado por FIX", async () => {
  vi.useFakeTimers();
  const api = fakeApi();
  const store = createPendingStore(localStorage, "test");
  store.save(request);
  vi.mocked(api.get).mockResolvedValue(accepted);
  const view = render(<App api={api} store={store} />);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });
  expect(screen.getByText("Ordem aceita")).toBeInTheDocument();
  expect(
    screen.getByText("Aguardando os eventos de auditoria."),
  ).toBeInTheDocument();
  vi.mocked(api.events).mockResolvedValue({
    available: true,
    events: [
      {
        eventId: crypto.randomUUID(),
        clOrdId: request.clOrdId,
        occurredAt: accepted.updatedAt,
        type: "OrderDecisionRecorded",
        description: "Decisão registrada após a resposta FIX.",
      },
    ],
  });
  await act(async () => {
    await vi.advanceTimersByTimeAsync(5000);
  });
  expect(
    screen.getByText("Decisão registrada após a resposta FIX."),
  ).toBeInTheDocument();
  expect(screen.getByText("Ordem aceita")).toBeInTheDocument();
  vi.mocked(api.events).mockRejectedValue(new Error("Kafka indisponível"));
  await act(async () => {
    await vi.advanceTimersByTimeAsync(5000);
  });
  expect(screen.getByText("Ordem aceita")).toBeInTheDocument();
  expect(
    screen.getByText(/Auditoria temporariamente indisponível/),
  ).toBeInTheDocument();
  view.unmount();
});
it.each(["B", "S"])(
  "envia lado %s ao selecionar a operação no formulário",
  async (side) => {
    const api = fakeApi();
    vi.mocked(api.submit).mockImplementation(async (value) => ({
      ...accepted,
      ...value,
    }));
    render(<App api={api} store={createPendingStore(localStorage, "test")} />);
    fireEvent.change(screen.getByLabelText("Lado da operação"), {
      target: { value: side },
    });
    fireEvent.change(screen.getByLabelText("Quantidade"), {
      target: { value: "10" },
    });
    fireEvent.change(screen.getByLabelText("Preço unitário (R$)"), {
      target: { value: "35,50" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Enviar ordem/ }));
    await waitFor(() =>
      expect(api.submit).toHaveBeenCalledWith(
        expect.objectContaining({ side }),
        expect.any(AbortSignal),
      ),
    );
  },
);
it("exibe erro de campo vindo do backend e permite corrigir e reenviar", async () => {
  const { createHttpApi } = await import("./api/http");
  const message =
    "Selecione Compra (B) ou Venda (S). O lado informado é inválido.";
  const errors = { side: [message] };
  const fetcher = vi
    .fn()
    .mockResolvedValue(
      new Response(JSON.stringify({ code: "ORDER_VALIDATION", errors }), {
        status: 400,
      }),
    );
  const api = fakeApi();
  api.submit = createHttpApi(1000, fetcher).submit;
  const store = createPendingStore(localStorage, "server-validation");
  render(<App api={api} store={store} />);
  fireEvent.change(screen.getByLabelText("Quantidade"), {
    target: { value: "10" },
  });
  fireEvent.change(screen.getByLabelText("Preço unitário (R$)"), {
    target: { value: "35,50" },
  });
  fireEvent.click(screen.getByRole("button", { name: /Enviar ordem/ }));
  await waitFor(() =>
    expect(
      screen.getByLabelText("Lado da operação"),
    ).toHaveAccessibleDescription(message),
  );
  expect(screen.getByLabelText("Lado da operação")).toHaveAttribute(
    "aria-invalid",
    "true",
  );
  expect(screen.getByLabelText("Lado da operação")).toHaveFocus();
  expect(screen.getByRole("button", { name: /Enviar ordem/ })).toBeEnabled();
  expect(
    screen.queryByRole("button", { name: /Tentar novamente com o mesmo ID/ }),
  ).not.toBeInTheDocument();
  expect(store.load()).toBeNull();
  expect(api.get).not.toHaveBeenCalled();
  const original = JSON.parse(fetcher.mock.calls[0][1].body);
  fetcher.mockImplementation(
    async (_url, options) =>
      new Response(
        JSON.stringify({ ...accepted, ...JSON.parse(options.body) }),
      ),
  );
  fireEvent.change(screen.getByLabelText("Lado da operação"), {
    target: { value: "S" },
  });
  fireEvent.click(screen.getByRole("button", { name: /Enviar ordem/ }));
  await screen.findByText("Ordem aceita");
  const corrected = JSON.parse(fetcher.mock.calls[1][1].body);
  expect(corrected.side).toBe("S");
  expect(corrected.clOrdId).not.toBe(original.clOrdId);
});
it("valida campos ao sair deles e bloqueia o envio de quantidade ou preço inválidos", () => {
  const api = fakeApi();
  render(
    <App api={api} store={createPendingStore(localStorage, "invalid-form")} />,
  );
  fireEvent.change(screen.getByLabelText("Quantidade"), {
    target: { value: "1.5" },
  });
  fireEvent.blur(screen.getByLabelText("Quantidade"));
  expect(screen.getByLabelText("Quantidade")).toHaveAccessibleDescription(
    expect.stringContaining("quantidade inteira"),
  );
  fireEvent.change(screen.getByLabelText("Preço unitário (R$)"), {
    target: { value: "abc" },
  });
  fireEvent.click(screen.getByRole("button", { name: /Enviar ordem/ }));
  expect(screen.getByLabelText("Preço unitário (R$)")).toHaveAttribute(
    "aria-invalid",
    "true",
  );
  expect(api.submit).not.toHaveBeenCalled();
});
