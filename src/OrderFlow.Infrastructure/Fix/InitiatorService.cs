using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix.Store;
using QuickFix.Transport;
namespace OrderFlow.Infrastructure.Fix;

public sealed class InitiatorService(GeneratorFixApplication app, IConfiguration configuration, ILoggerFactory logs) : IHostedService, IDisposable
{
    private SocketInitiator? _initiator;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = FixSettings.Load(configuration);

        _initiator = new SocketInitiator(app, new FileStoreFactory(settings), settings, logs, new QuickFix.FIX44.MessageFactory());

        _initiator.Start(); return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) { _initiator?.Stop(); return Task.CompletedTask; }

    public void Dispose() => _initiator?.Dispose();
}
// Durable submission queue, single sender per FIX identity. No DB transaction spans network I/O.
