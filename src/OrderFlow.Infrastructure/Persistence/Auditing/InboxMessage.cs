namespace OrderFlow.Infrastructure.Persistence.Auditing;

public sealed class InboxMessage
{
    public string Consumer { get; set; } = "";

    public Guid EventId { get; set; }

    public string PayloadHash { get; set; } = "";

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
