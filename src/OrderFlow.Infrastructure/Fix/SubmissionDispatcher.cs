using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Orders;

namespace OrderFlow.Infrastructure.Fix;

public sealed class SubmissionDispatcher(IServiceScopeFactory scopes, IOrderTransport transport,
    ILogger<SubmissionDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!transport.IsLoggedOn) continue;

            try
            {
                using var scope = scopes.CreateScope();

                var store = scope.ServiceProvider.GetRequiredService<ISubmissionStore>();

                foreach (var item in await store.PendingAsync(stoppingToken))
                {
                    await store.MarkAttemptAsync(item.ClOrdId, stoppingToken);
                    if (!transport.Send(new(item.ClOrdId, item.Symbol, item.Side, item.Quantity, item.Price, item.AccountId), item.CreatedAt)) break;
                }
            }

            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }

            catch (Exception ex) { logger.LogError(ex, "Falha no envio; submissões persistidas serão tentadas novamente"); }
        }
    }
}
