using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public interface IOrderProcessor
{
    Task<OrderDecision> ProcessAsync(string session, OrderRequest request, string? protocolBusinessError = null,
        CancellationToken cancellationToken = default);
}
