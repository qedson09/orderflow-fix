using Microsoft.EntityFrameworkCore.Storage;
using OrderFlow.Application.Orders;

namespace OrderFlow.Infrastructure.Persistence.Orders;

internal sealed class PostgresOrderTransaction(IDbContextTransaction transaction) : IOrderTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
