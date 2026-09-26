using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public interface IOrderTransport
{
    bool IsLoggedOn { get; }
    bool Send(OrderRequest request, DateTime createdAt);
}
