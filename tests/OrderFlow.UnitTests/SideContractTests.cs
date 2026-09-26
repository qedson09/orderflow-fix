using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Fix;
using OrderFlow.Infrastructure.Persistence.Configuration;
using OrderFlow.Infrastructure.Persistence.Orders;

namespace OrderFlow.UnitTests;

public sealed class SideContractTests
{
    [Theory]
    [InlineData("B", OrderSide.Buy, '1')]
    [InlineData("S", OrderSide.Sell, '2')]
    public void ApiFixReportEventAndDatabaseUseMatchingSides(string code, OrderSide side, char fixSide)
    {
        var request = new OrderInput(Guid.NewGuid().ToString("D"), "PETR4", code, 10, "35.50").ToDomain();

        Assert.Equal(side, request.Side);

        var message = FixMapper.ToMessage(request, DateTime.UtcNow);

        Assert.Equal(fixSide, message.Side.Value);

        Assert.Equal(request, FixMapper.FromMessage(message));

        var order = Order.Decide("test", request, null, null);

        var decision = OrderDecision.From(order);

        Assert.Equal(side, FixMapper.FromMessage(FixMapper.ToMessage(decision)).Side);

        Assert.Equal(code, OrderDecisionEvent.Create(order, null).Side);

        var view = new SubmissionView(request.ClOrdId, request.Symbol, side, 10, 35.50m,
            "Pending", null, null, null, DateTime.UtcNow, null);

        Assert.Equal(code, ApiOrder.From(view).Side);

        using var accumulator = new AccumulatorDesignFactory().CreateDbContext([]);

        using var generator = new GeneratorDesignFactory().CreateDbContext([]);

        foreach (var property in new[] {
            accumulator.Model.FindEntityType(typeof(Order))!.FindProperty(nameof(Order.Side))!,
            generator.Model.FindEntityType(typeof(Submission))!.FindProperty(nameof(Submission.Side))!
        })
        {
            Assert.Equal(1, property.GetMaxLength());

            var converter = property.GetTypeMapping().Converter!;

            Assert.Equal(code, converter.ConvertToProvider(side));

            Assert.Equal(side, converter.ConvertFromProvider(code));
        }
    }

    [Theory]
    [InlineData("Buy")][InlineData("Sell")][InlineData("b")][InlineData("s")]
    [InlineData("1")][InlineData("2")][InlineData("")][InlineData("BB")]
    public void ApiRejectsUnsupportedSide(string side) => Assert.Throws<OrderValidationException>(() =>
        new OrderInput(Guid.NewGuid().ToString("D"), "PETR4", side, 1, "35.50").ToDomain());
}
