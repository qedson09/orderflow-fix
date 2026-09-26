namespace OrderFlow.Infrastructure.Messaging;

public sealed class AuditHealth
{
    private int available;

    public bool Available => Volatile.Read(ref available) == 1;

    public void Set(bool value) => Volatile.Write(ref available, value ? 1 : 0);
}
