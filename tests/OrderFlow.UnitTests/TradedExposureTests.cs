using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Customers;
using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence.Configuration;

namespace OrderFlow.UnitTests;

public class TradedExposureTests
{
    [Fact]
    public void OnlyAcceptedAssetsOfSelectedAccountAppearIncludingClosedPosition()
    {
        var rows = CustomerCatalog.All.SelectMany(c => OrderRules.Symbols.Select(s => new SymbolExposure(s, c.Id))).ToList();

        Assert.Empty(rows.AsQueryable().TradedBy("CLIENTE-001"));

        var petr = rows.Single(x => x.AccountId == "CLIENTE-001" && x.Symbol == "PETR4");

        var buy = new OrderRequest(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 100, 35);

        Assert.True(petr.TryApply(buy, 100_000_000m));

        Assert.True(petr.TryApply(buy with { ClOrdId = Guid.NewGuid().ToString("D"), Side = OrderSide.Sell }, 100_000_000m));

        Assert.Equal(0, petr.Amount);

        rows.Single(x => x.AccountId == "CLIENTE-001" && x.Symbol == "VIIA4").RegisterDecision();

        var other = rows.Single(x => x.AccountId == "CLIENTE-002" && x.Symbol == "VALE3");

        Assert.True(other.TryApply(buy with { AccountId = "CLIENTE-002", Symbol = "VALE3" }, 100_000_000m));

        Assert.Equal(new[] { "PETR4" }, rows.AsQueryable().TradedBy("CLIENTE-001").Select(x => x.Symbol));

        Assert.Equal(new[] { "VALE3" }, rows.AsQueryable().TradedBy("CLIENTE-002").Select(x => x.Symbol));
    }

    [Fact]
    public void FilterIsTranslatedToPostgresRatherThanAppliedOnlyInBrowser()
    {
        using var db = new AccumulatorDesignFactory().CreateDbContext([]);

        var sql = db.Exposures.AsNoTracking().TradedBy("CLIENTE-002").ToQueryString();

        Assert.Contains("\"AccountId\"", sql);

        Assert.Contains("\"Version\" > 0", sql);

        Assert.Contains("ORDER BY", sql);
    }
}
