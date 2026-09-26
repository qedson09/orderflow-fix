using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Fix;
using QuickFix.Fields;
namespace OrderFlow.UnitTests;
public sealed class FixMapperTests
{
    [Theory] [InlineData(OrderSide.Buy, '1')] [InlineData(OrderSide.Sell, '2')]
    public void NewOrderMapsAllBusinessFields(OrderSide side, char tagValue)
    {
        var request = new OrderRequest(Guid.NewGuid().ToString("D"), "VIIA4", side, 99, 12.34m);

        var message = FixMapper.ToMessage(request, DateTime.UtcNow);

        Assert.Equal("D", message.Header.GetString(Tags.MsgType));

        Assert.Equal(tagValue, message.Side.Value); Assert.Equal(OrdType.LIMIT, message.OrdType.Value);

        Assert.Equal(TimeInForce.DAY, message.TimeInForce.Value); Assert.Equal(request, FixMapper.FromMessage(message));
    }

    [Theory] [InlineData(null, '0', 100)] [InlineData("Limite excedido", '8', 0)]
    public void ReportsHaveCoherentUnfilledFields(string? reason, char expectedType, decimal leaves)
    {
        var request = new OrderRequest(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 100, 35m);

        var decision = OrderDecision.From(Order.Decide("FIX.4.4:X->Y", request, reason));

        var report = FixMapper.ToMessage(decision);

        Assert.Equal("8", report.Header.GetString(Tags.MsgType));

        Assert.Equal(expectedType, report.ExecType.Value); Assert.Equal(expectedType, report.OrdStatus.Value);

        Assert.Equal(0, report.CumQty.Value); Assert.Equal(0, report.AvgPx.Value); Assert.Equal(leaves, report.LeavesQty.Value);

        Assert.Equal(decision, FixMapper.FromMessage(report));
    }
}
