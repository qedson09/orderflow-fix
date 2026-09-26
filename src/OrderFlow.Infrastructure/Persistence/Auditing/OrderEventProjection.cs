namespace OrderFlow.Infrastructure.Persistence.Auditing;

public sealed class OrderEventProjection
{
    public string Source { get; set; } = "Kafka";

    public Guid EventId { get; set; }

    public string ClOrdId { get; set; } = "";

    public string Symbol { get; set; } = "";

    public long? SymbolSequence { get; set; }

    public DateTime OccurredAt { get; set; }

    public string Type { get; set; } = "";

    public string Description { get; set; } = "";
}
