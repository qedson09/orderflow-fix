using OrderFlow.Application.Orders;
using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;

namespace OrderFlow.UnitTests.Exposures;

internal sealed class TestAccumulatorStore : IAccumulatorUnitOfWork
{
    public SymbolExposure Exposure { get; } = new("PETR4");

    public Order? Order { get; private set; }

    public TestOrderTransaction Transaction { get; private set; } = new();

    public int Events { get; private set; }

    public Task<IOrderTransaction> BeginAsync(string session, string clOrdId, CancellationToken cancellationToken)
    {
        Transaction = new();
        return Task.FromResult<IOrderTransaction>(Transaction);
    }

    public Task<Order?> FindOrderAsync(string session, string clOrdId, CancellationToken cancellationToken) => Task.FromResult(Order);

    public Task<SymbolExposure> LockExposureAsync(string symbol, CancellationToken cancellationToken, string accountId = "CLIENTE-001") => Task.FromResult(Exposure);

    public void Add(Order order) => Order = order;

    public void AddEvent(OrderDecisionEvent message) => Events++;

    public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
