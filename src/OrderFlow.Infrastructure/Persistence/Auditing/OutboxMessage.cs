using OrderFlow.Application.Orders;
using System.Text.Json;

namespace OrderFlow.Infrastructure.Persistence.Auditing;

public sealed class OutboxMessage
{
    public Guid EventId { get; set; }

    public string PartitionKey { get; set; } = "";

    public long? SymbolSequence { get; set; }

    public string Payload { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public static OutboxMessage From(OrderDecisionEvent message) => new()
    {
        EventId = message.EventId, PartitionKey = message.SymbolSequence is null ? "validation-errors" : message.AccountId + ":" + message.Symbol,
        SymbolSequence = message.SymbolSequence, Payload = JsonSerializer.Serialize(message, OrderDecisionEvent.Json),
        CreatedAt = message.OccurredAtUtc, NextAttemptAt = message.OccurredAtUtc
    };
}
