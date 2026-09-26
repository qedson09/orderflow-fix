using OrderFlow.Application.Exposures;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Fix;
namespace OrderFlow.UnitTests;

public class AccountExposureTests
{
    [Fact]
    public async Task AccountsAreIsolatedAndSummaryHasDirectionalCapacity()
    {
        var first = new SymbolExposure("PETR4", "CLIENTE-001");

        var second = new SymbolExposure("PETR4", "CLIENTE-002");

        var request = new OrderRequest(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 25000, 999, "CLIENTE-001");

        Assert.True(first.TryApply(request, 100_000_000m));

        Assert.Equal(0, second.Amount);

        var summary = ExposureSummary.From(first, 100_000_000m);

        Assert.Equal("24975000.00", summary.CurrentExposure);

        Assert.Equal("75025000.00", summary.AvailableForBuy);

        Assert.Equal("124975000.00", summary.AvailableForSell);

        await Assert.ThrowsAsync<ArgumentException>(async () => await Task.Run(() => second.TryApply(request, 100_000_000m)));
    }

    [Fact]
    public void NegativeExposureIncreasesBuyCapacity()
    {
        var exposure = new SymbolExposure("VALE3");

        exposure.TryApply(new(Guid.NewGuid().ToString("D"), "VALE3", OrderSide.Sell, 100, 35), 100_000_000m);

        var summary = ExposureSummary.From(exposure, 100_000_000m);

        Assert.Equal("-3500.00", summary.CurrentExposure);

        Assert.Equal("100003500.00", summary.AvailableForBuy);

        Assert.Equal("99996500.00", summary.AvailableForSell);
    }

    [Fact]
    public void AccountIsCarriedByFixAndIncludedInIdentity()
    {
        var request = new OrderRequest(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 100, 35, "CLIENTE-002");

        Assert.Equal(request, FixMapper.FromMessage(FixMapper.ToMessage(request, DateTime.UtcNow)));

        var order = Order.Decide("FIX", request, null);

        Assert.False(order.Matches(request with { AccountId = "CLIENTE-001" }));

        var decision = OrderDecision.From(order);

        Assert.Equal(decision, FixMapper.FromMessage(FixMapper.ToMessage(decision)));
    }
}
