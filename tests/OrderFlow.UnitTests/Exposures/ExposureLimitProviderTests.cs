using OrderFlow.Application.Exposures;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Exposures;

namespace OrderFlow.UnitTests.Exposures;

public sealed class ExposureLimitProviderTests
{
    private static OrderRequest Request(decimal quantity = 10) => new(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, quantity, 10m);

    [Fact]
    public async Task Fixed_provider_keeps_challenge_limit_for_each_customer_and_symbol()
    {
        var provider = new FixedExposureLimitProvider();

        foreach (var account in new[] { "CLIENTE-001", "CLIENTE-002" })
            foreach (var symbol in OrderRules.Symbols)
                Assert.Equal(100_000_000m, await provider.GetLimitAsync(account, symbol));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetLimitAsync("CLIENTE-001", "PETR4", new CancellationToken(true)));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(99.99, false)]
    public async Task Processing_uses_injected_limit_and_passes_customer_and_symbol(decimal limit, bool accepted)
    {
        var store = new TestAccumulatorStore();

        var provider = new TestLimitProvider(limit);

        await new ProcessOrder(store, provider).ProcessAsync("session", Request());

        Assert.Equal(accepted ? 100m : 0m, store.Exposure.Amount);

        Assert.Equal("CLIENTE-001", provider.AccountId);

        Assert.Equal("PETR4", provider.Symbol);

        Assert.True(store.Transaction.Committed);

        Assert.Equal(1, store.Events);

        if (!accepted) 
            Assert.Contains("99,99", store.Order!.Reason);
    }

    [Fact]
    public async Task Duplicate_preserves_decision_without_querying_provider_again()
    {
        var store = new TestAccumulatorStore();

        var provider = new TestLimitProvider(100m);

        var processor = new ProcessOrder(store, provider);

        var request = Request();

        var first = await processor.ProcessAsync("session", request);

        provider.Limit = 1m;

        provider.Fail = true;

        var second = await processor.ProcessAsync("session", request);

        Assert.Equal(first, second);

        Assert.Equal(1, provider.Calls);

        Assert.Equal(1, store.Events);

        Assert.Equal(100m, store.Exposure.Amount);
    }

    [Fact]
    public async Task Provider_failure_does_not_persist_a_rejection()
    {
        var store = new TestAccumulatorStore();

        await Assert.ThrowsAsync<IOException>(() => new ProcessOrder(store, new TestLimitProvider(100m) { Fail = true }).ProcessAsync("session", Request()));

        Assert.Null(store.Order);

        Assert.Equal(0, store.Events);

        Assert.Equal(0m, store.Exposure.Amount);

        Assert.False(store.Transaction.Committed);
    }

    [Fact]
    public void Summary_uses_effective_limit()
    {
        var exposure = new SymbolExposure("PETR4");

        exposure.TryApply(Request(), 200m);

        var summary = ExposureSummary.From(exposure, 200m);

        Assert.Equal("200.00", summary.AbsoluteLimit);

        Assert.Equal("100.00", summary.AvailableForBuy);

        Assert.Equal("300.00", summary.AvailableForSell);

        Assert.Equal(50m, summary.UtilizationPercentage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_limit_cannot_change_exposure(decimal limit)
    {
        var exposure = new SymbolExposure("PETR4");

        Assert.Throws<ArgumentOutOfRangeException>(() => exposure.TryApply(Request(), limit));

        Assert.Throws<ArgumentOutOfRangeException>(() => ExposureSummary.From(exposure, limit));

        Assert.Equal(0m, exposure.Amount);
    }
}
