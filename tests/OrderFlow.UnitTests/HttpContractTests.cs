using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.UnitTests;

public sealed class HttpContractTests
{
    [Theory]
    [InlineData("1.٠١")] [InlineData("35,50")] [InlineData("1e2")] [InlineData("1.001")] [InlineData("0.00")] [InlineData("1000.00")]
    public void InvalidMoneyStringIsRefused(string price) => Assert.Throws<OrderValidationException>(() =>
        new OrderInput(Guid.NewGuid().ToString("D"), "PETR4", "B", 1, price).ToDomain());
    
    [Fact] 
    public void FrontendUuidAndPriceArePreserved()
    {
        var id = Guid.NewGuid().ToString("D");

        var result = new OrderInput(id, "VALE3", "S", 99, "35.50").ToDomain();

        Assert.Equal(id, result.ClOrdId); 
        
        Assert.Equal(35.50m, result.Price); 
        
        Assert.Equal(OrderSide.Sell, result.Side);
    }

    [Fact]
    public void PendingHasNoExecutionReportAndAcceptedRequiresPersistedFix()
    {
        var row = new SubmissionView(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 100, 35.5m, "Pending", null, null, null, DateTime.UtcNow, null);

        var pending = ApiOrder.From(row); Assert.Equal("Pending", pending.Status); 
        
        Assert.Null(pending.ExecutionReport);

        var accepted = ApiOrder.From(row with { Status = "New", OrderId = Guid.NewGuid().ToString(), ExecId = "exec-1", UpdatedAt = DateTime.UtcNow });

        Assert.Equal("Accepted", accepted.Status); Assert.Equal("35.50", accepted.Price); 
        
        Assert.Equal("New", accepted.ExecutionReport!.ExecType);
    }
}
