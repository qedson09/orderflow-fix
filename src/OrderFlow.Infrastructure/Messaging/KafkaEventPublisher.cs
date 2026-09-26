using Confluent.Kafka;
using OrderFlow.Application.Orders;
namespace OrderFlow.Infrastructure.Messaging;

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> producer;

    private readonly KafkaSettings settings;

    public KafkaEventPublisher(KafkaSettings settings)
    {
        this.settings = settings;

        producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers, EnableIdempotence = true, Acks = Acks.All,
            MessageTimeoutMs = 5000, SocketTimeoutMs = 5000, AllowAutoCreateTopics = false
        }).Build();
    }

    public async Task PublishAsync(string key, string payload, CancellationToken ct)
    {
        var result = await producer.ProduceAsync(settings.Topic, new Message<string, string> { Key = key, Value = payload }, ct);

        if (result.Status != PersistenceStatus.Persisted) throw new InvalidOperationException("Broker não confirmou persistência");
    }

    public void Dispose() => producer.Dispose();
}
