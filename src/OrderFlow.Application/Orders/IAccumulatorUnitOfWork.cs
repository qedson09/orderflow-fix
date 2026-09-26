using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Exposures;

namespace OrderFlow.Application.Orders;

public interface IAccumulatorUnitOfWork
{
    Task<IOrderTransaction> BeginAsync(string session, string clOrdId, CancellationToken cancellationToken);

    Task<Order?> FindOrderAsync(string session, string clOrdId, CancellationToken cancellationToken);

    Task<SymbolExposure> LockExposureAsync(string symbol, CancellationToken cancellationToken, string accountId = "CLIENTE-001");

    void Add(Order order);

    void AddEvent(OrderDecisionEvent message);

    Task SaveAsync(CancellationToken cancellationToken);
}
