using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;

namespace OrderFlow.UnitTests;

public sealed class OrderRulesTests
{
    private static OrderRequest Request(decimal qty = 100m, decimal price = 35m, OrderSide side = OrderSide.Buy, string symbol = "PETR4") =>
        new(Guid.NewGuid().ToString("D"), symbol, side, qty, price);

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(100000)] [InlineData(1.5)]
    public void InvalidQuantities(decimal quantity) => Assert.NotNull(OrderRules.Validate(Request(qty: quantity)));

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(1000)] [InlineData(1.001)]
    public void InvalidPrices(decimal price) => Assert.NotNull(OrderRules.Validate(Request(price: price)));

    [Theory]
    [InlineData(1,0.01)] [InlineData(99999,999.99)]
    public void ValidBoundaries(decimal quantity, decimal price) => Assert.Null(OrderRules.Validate(Request(quantity, price)));

    [Fact]
    public void InvalidSymbol() => Assert.NotNull(OrderRules.Validate(Request(symbol:"ABCD3")));

    [Fact]
    public void InvalidSide() => Assert.NotNull(OrderRules.Validate(Request(side:(OrderSide)7)));

    [Theory] [InlineData(OrderSide.Buy, 3500)] [InlineData(OrderSide.Sell, -3500)]
    public void SignedExposure(OrderSide side, decimal expected)
    {
        var exposure = new SymbolExposure("PETR4");

        Assert.True(exposure.TryApply(Request(side:side), 100_000_000m)); Assert.Equal(expected, exposure.Amount);
    }

    [Theory] [InlineData(OrderSide.Buy, 100000000)] [InlineData(OrderSide.Sell, -100000000)]
    public void ExactLimitAcceptedAndOneCentBeyondRejected(OrderSide side, decimal expected)
    {
        var exposure = new SymbolExposure("PETR4");

        Assert.True(exposure.TryApply(Request(99999, 999, side), 100_000_000m));

        Assert.True(exposure.TryApply(Request(1, 999, side), 100_000_000m));

        Assert.True(exposure.TryApply(Request(1000, 100, side), 100_000_000m));

        Assert.Equal(expected, exposure.Amount);

        Assert.False(exposure.TryApply(Request(1, 0.01m, side), 100_000_000m));

        Assert.Equal(expected, exposure.Amount); Assert.Equal(3, exposure.Version);
    }

    [Fact]
    public void OppositeSideCanReverseSign()
    {
        var exposure = new SymbolExposure("PETR4");

        exposure.TryApply(Request(100,10), 100_000_000m); exposure.TryApply(Request(200,10,OrderSide.Sell), 100_000_000m);

        Assert.Equal(-1000, exposure.Amount);
    }

    [Fact]
    public void SymbolsAreIsolated()
    {
        var petr = new SymbolExposure("PETR4"); 
        
        var vale = new SymbolExposure("VALE3");

        petr.TryApply(Request(), 100_000_000m); Assert.Equal(0, vale.Amount);

        Assert.Throws<ArgumentException>(()=>vale.TryApply(Request(), 100_000_000m));
    }
}
