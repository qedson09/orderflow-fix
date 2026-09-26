namespace OrderFlow.Infrastructure.Messaging;

public sealed class PoisonMessageException(string message) : Exception(message);
