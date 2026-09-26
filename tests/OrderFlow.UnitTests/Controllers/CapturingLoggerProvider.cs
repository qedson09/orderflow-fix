using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OrderFlow.UnitTests.Controllers;

internal sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
{
    public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => this;

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Enqueue((logLevel, formatter(state, exception), exception));

    public void Dispose() { }
}
