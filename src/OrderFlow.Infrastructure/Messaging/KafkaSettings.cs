using Microsoft.Extensions.Configuration;

namespace OrderFlow.Infrastructure.Messaging;

public sealed record KafkaSettings(string BootstrapServers, string Topic)
{
    public static KafkaSettings From(IConfiguration config) => new(config["Kafka:BootstrapServers"] ?? "localhost:9092",
        config["Kafka:Topic"] ?? "orderflow.order-decisions.v1");
}
