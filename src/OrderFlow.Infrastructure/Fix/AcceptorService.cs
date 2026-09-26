using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Store;
namespace OrderFlow.Infrastructure.Fix;

public sealed class AcceptorService(AccumulatorFixApplication app, IConfiguration configuration, ILoggerFactory logs) : IHostedService, IDisposable
{
    private ThreadedSocketAcceptor? _acceptor;

    public bool Started { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = FixSettings.Load(configuration);

        _acceptor = new ThreadedSocketAcceptor(app, new FileStoreFactory(settings), settings, logs, new QuickFix.FIX44.MessageFactory());

        _acceptor.Start(); Started = true; return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) { Started = false; _acceptor?.Stop(); return Task.CompletedTask; }

    public void Dispose() => _acceptor?.Dispose();
}
