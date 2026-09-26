namespace OrderFlow.Infrastructure.Persistence.Auditing;

public sealed class FixOutbox
{
    public string ClOrdId { get; set; } = "";

    public int Attempts { get; set; }

    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}
