using OrderFlow.Application.Orders;

namespace OrderFlow.UnitTests.Exposures;

internal sealed class TestOrderTransaction : IOrderTransaction
{
    public bool Committed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken) { Committed = true; return Task.CompletedTask; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
