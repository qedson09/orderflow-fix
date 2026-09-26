namespace OrderFlow.Infrastructure.Persistence.Auditing;

public sealed class QuarantinedMessage
{
    public string Topic { get; set; } = "";

    public int Partition { get; set; }

    public long Offset { get; set; }

    public string Payload { get; set; } = "";

    public string Error { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
