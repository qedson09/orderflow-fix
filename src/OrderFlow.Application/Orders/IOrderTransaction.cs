using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public interface IOrderTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
