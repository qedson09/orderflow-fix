using OrderFlow.Domain.Customers;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Domain.Exposures;

public sealed class SymbolExposure
{
    public string AccountId { get; private set; } = "CLIENTE-001";
    public string Symbol { get; private set; } = "";
    public decimal Amount { get; private set; }
    public long Version { get; private set; }
    public long DecisionSequence { get; private set; }
    public void RegisterDecision() => DecisionSequence++;
    private SymbolExposure() { }
    public SymbolExposure(string symbol, string accountId = "CLIENTE-001")
    {
        if (!CustomerCatalog.Contains(accountId)) throw new ArgumentException("Cliente inválido.", nameof(accountId));
        AccountId = accountId;
        if (!OrderRules.Symbols.Contains(symbol)) throw new ArgumentException("Símbolo inválido.", nameof(symbol));
        Symbol = symbol;
    }
    public bool TryApply(OrderRequest request, decimal limit)
    {
        if (request.AccountId != AccountId || request.Symbol != Symbol || OrderRules.Validate(request) is not null) throw new ArgumentException("Ordem inválida.");
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "O limite deve ser positivo.");
        var projected = OrderRules.Project(Amount, request);
        if (Math.Abs(projected) > limit) return false;
        Amount = projected;
        Version++;
        return true;
    }
}
