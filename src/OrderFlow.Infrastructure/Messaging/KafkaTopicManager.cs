using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace OrderFlow.Infrastructure.Messaging;

public sealed class KafkaTopicManager(KafkaSettings settings)
{
    public async Task EnsureAsync(CancellationToken ct)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = settings.BootstrapServers, SocketTimeoutMs = 5000 }).Build();

        try
        {
            await admin.CreateTopicsAsync(
            [
                new TopicSpecification
                {
                    Name = settings.Topic,
                    NumPartitions = 3,
                    ReplicationFactor = 1,
                    Configs = new Dictionary<string, string>
                    {
                        ["retention.ms"] = "604800000",
                        ["cleanup.policy"] = "delete"
                    }
                }
            ], new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(5) }).WaitAsync(ct);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Log or handle the case where the topic already exists
            Console.WriteLine($"Topic '{settings.Topic}' already exists.");
        }
    }
}
