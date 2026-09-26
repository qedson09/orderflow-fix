using OrderFlow.Domain.Customers;

namespace OrderFlow.Domain.Orders;

public static class OrderRules
{
    public static IReadOnlyList<string> Symbols { get; } = Array.AsReadOnly(new[] { "PETR4", "VALE3", "VIIA4" });
    public static string? Validate(OrderRequest request)
    {
        if (!CustomerCatalog.Contains(request.AccountId)) return "Cliente/conta desconhecido.";
        if (!Guid.TryParseExact(request.ClOrdId, "D", out _)) return "ClOrdID deve ser um UUID no formato D (36 caracteres).";
        if (!Symbols.Contains(request.Symbol)) return "Símbolo permitido: PETR4, VALE3 ou VIIA4.";
        if (!Enum.IsDefined(request.Side)) return "Lado deve ser Compra ou Venda.";
        if (request.Quantity <= 0 || request.Quantity >= 100_000 || decimal.Truncate(request.Quantity) != request.Quantity)
            return "Quantidade deve ser inteira, de 1 a 99.999.";
        if (request.Price <= 0 || request.Price >= 1_000 || request.Price % 0.01m != 0)
            return "Preço deve ser de 0,01 a 999,99, em múltiplos de 0,01.";
        return null;
    }
    public static decimal Project(decimal current, OrderRequest request) =>
        current + (request.Side == OrderSide.Buy ? 1m : -1m) * request.Price * request.Quantity;
}
