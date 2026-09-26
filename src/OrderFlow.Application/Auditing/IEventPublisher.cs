namespace OrderFlow.Application.Orders;

public interface IEventPublisher
{
    Task PublishAsync(string key, string payload, CancellationToken cancellationToken);
}
