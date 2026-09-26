using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Persistence.Configuration;
using OrderFlow.Infrastructure.Persistence.Orders;
using Testcontainers.Kafka;
namespace OrderFlow.IntegrationTests;

[Collection("Postgres")]
public sealed class KafkaRecoveryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task RealBrokerOutageDuplicatePublicationAndConsumerRestartPreserveExactlyOneAuditRow()
    {
        await fixture.ResetAsync();

        await using var broker = new KafkaBuilder().WithImage("confluentinc/cp-kafka:7.8.0").Build();

        await broker.StartAsync();

        var settings = new KafkaSettings(broker.GetBootstrapAddress(), "orderflow-test-" + Guid.NewGuid().ToString("N"));

        var topics = new KafkaTopicManager(settings);

        await topics.EnsureAsync(default);

        var request = new OrderRequest(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 100, 35.50m);

        await using (var a = fixture.Accumulator())

            await new ProcessOrder(new PostgresAccumulatorUnitOfWork(a), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("test", request);

        await using (var g = fixture.Generator()) await new PostgresSubmissionStore(g).CreateAsync(request, default);

        using var producer = new KafkaEventPublisher(settings);

        await broker.StopAsync();

        await using (var a = fixture.Accumulator())
        {
            await new OutboxPump(a, producer, NullLogger<OutboxPump>.Instance).PublishOnceAsync(default);

            Assert.Null((await a.Outbox.SingleAsync()).PublishedAt);

            Assert.Equal(OrderStatus.New, (await a.Orders.SingleAsync()).Status);
        }

        await broker.StartAsync();

        await topics.EnsureAsync(default);

        await ResetRetry();

        // Entrega ao broker seguida de falha antes de marcar PublishedAt.
        await using (var a = fixture.Accumulator())
            await new OutboxPump(a, new LostAck(producer), NullLogger<OutboxPump>.Instance).PublishOnceAsync(default);
        
        await ResetRetry();

        await using (var a = fixture.Accumulator())
            Assert.Equal(1, await new OutboxPump(a, producer, NullLogger<OutboxPump>.Instance).PublishOnceAsync(default));

        // Primeiro consumidor persiste a inbox, mas termina sem confirmar offset.
        using (var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers, GroupId = AuditInbox.ConsumerName,
            AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false, EnableAutoOffsetStore = false
        }).Build())
        {
            consumer.Subscribe(settings.Topic);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            var record = consumer.Consume(timeout.Token);

            await using var db = fixture.Generator();

            await new AuditInbox(db, NullLogger<AuditInbox>.Instance).AcceptAsync(record.Message.Value, timeout.Token);

            consumer.Close();
        }

        // O worker real usa o mesmo grupo, recebe replay e confirma só após commit.
        await using var services = new ServiceCollection().AddLogging()
            .AddGeneratorDatabase(fixture.ConnectionString).AddScoped<AuditInbox>()
            .AddSingleton(settings).AddSingleton<KafkaTopicManager>().AddSingleton<AuditHealth>()
            .AddSingleton<AuditConsumer>().BuildServiceProvider();

        var worker = services.GetRequiredService<AuditConsumer>();

        await worker.StartAsync(default);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            using var inspector = new ConsumerBuilder<string, string>(new ConsumerConfig
            { BootstrapServers = settings.BootstrapServers, GroupId = AuditInbox.ConsumerName, EnableAutoCommit = false }).Build();

            while (true)
            {
                var offsets = inspector.Committed(Enumerable.Range(0, 3).Select(i => new TopicPartition(settings.Topic, i)), TimeSpan.FromSeconds(5));

                if (offsets.Any(x => x.Offset.Value >= 2)) break;

                await Task.Delay(250, timeout.Token);
            }

            await using var g = fixture.Generator();

            Assert.Equal(1, await g.Inbox.CountAsync()); Assert.Equal(1, await g.Events.CountAsync(x => x.Source == "Kafka"));

            Assert.Equal("Pending", (await g.Submissions.SingleAsync()).Status);
        }
        finally
        {
            await worker.StopAsync(default);
            worker.Dispose();
        }
    }

    private async Task ResetRetry()
    {
        await using var db = fixture.Accumulator();

        await db.Outbox.ExecuteUpdateAsync(u => u.SetProperty(x => x.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)));
    }

    private sealed class LostAck(IEventPublisher inner) : IEventPublisher
    {
        public async Task PublishAsync(string key, string payload, CancellationToken cancellationToken)
        {
            await inner.PublishAsync(key, payload, cancellationToken);
            throw new IOException("Process failed before marking publication");
        }
    }
}
