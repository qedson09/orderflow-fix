using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace OrderFlow.Infrastructure.Messaging;

public sealed class OutboxPublisher(IServiceScopeFactory scopes, KafkaTopicManager topics, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Kafka não bloqueia startup, engine FIX ou resposta de negócio.

        var topicReady = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!topicReady) { await topics.EnsureAsync(stoppingToken); topicReady = true; }
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OutboxPump>().PublishOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) 
            { 
                break; 
            }
            catch (Exception ex) 
            { 
                topicReady = false; logger.LogWarning(ex, "Outbox aguardando recuperação"); 
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
