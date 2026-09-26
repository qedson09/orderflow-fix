using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Infrastructure.Messaging;

public sealed class AuditConsumer(IServiceScopeFactory scopes, KafkaSettings settings, KafkaTopicManager topics,
    AuditHealth health, ILogger<AuditConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await topics.EnsureAsync(stoppingToken);

                using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
                {
                    BootstrapServers = settings.BootstrapServers, GroupId = AuditInbox.ConsumerName,
                    EnableAutoCommit = false, EnableAutoOffsetStore = false, AutoOffsetReset = AutoOffsetReset.Earliest,
                    AllowAutoCreateTopics = false, SessionTimeoutMs = 10000, MaxPollIntervalMs = 300000
                }).SetErrorHandler((_, error) => { if (error.IsError) health.Set(false); }).Build();

                consumer.Subscribe(settings.Topic); health.Set(true);

                var checkedAt = DateTime.UtcNow;

                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (DateTime.UtcNow - checkedAt > TimeSpan.FromSeconds(30))
                        { 
                            await topics.EnsureAsync(stoppingToken); health.Set(true); 
                            
                            checkedAt = DateTime.UtcNow; 
                        }

                        var record = consumer.Consume(TimeSpan.FromMilliseconds(500));

                        if (record is null) continue;

                        using var scope = scopes.CreateScope();

                        var inbox = scope.ServiceProvider.GetRequiredService<AuditInbox>();

                        try 
                        { 
                            await inbox.AcceptAsync(record.Message.Value ?? "", stoppingToken); 
                        }
                        catch (PoisonMessageException ex)
                        {
                            // Commit do offset somente depois de preservar a mensagem problemática.
                            await inbox.QuarantineAsync(record.Topic, record.Partition.Value, record.Offset.Value,
                                record.Message.Value ?? "", ex.Message, stoppingToken);
                        }

                        // Crash após o commit PostgreSQL e antes deste ponto causa nova entrega deduplicada.
                        consumer.Commit(record); health.Set(true);
                    }
                }
                finally { consumer.Close(); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) 
            { 
                break; 
            }
            catch (Exception ex) 
            { 
                health.Set(false); logger.LogWarning(ex, "Auditoria aguardando recuperação; offset não confirmado será reentregue"); 
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
