using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Domain.Orders;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Persistence.Orders;
using OrderFlow.Infrastructure.Persistence.Configuration;

namespace OrderFlow.IntegrationTests;

[Collection("Postgres")]
public sealed class ReliabilityTests(PostgresFixture fixture)
{
    private static OrderRequest Request(string symbol = "PETR4") => new(Guid.NewGuid().ToString("D"), symbol, OrderSide.Buy, 100, 35.50m);

    private async Task<OrderDecision> Decide(OrderRequest request)
    {
        await using var db = fixture.Accumulator();

        return await new ProcessOrder(new PostgresAccumulatorUnitOfWork(db), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("FIX.4.4:ACCUMULATOR->GENERATOR", request);
    }

    [Fact]
    public async Task FinancialCommitContainsDecisionExposureAndExactlyOneOutboxEvent()
    {
        await fixture.ResetAsync(); var request = Request(); await Decide(request); await Decide(request);

        await using var db = fixture.Accumulator();

        Assert.Equal(1, await db.Orders.CountAsync()); Assert.Equal(1, await db.Outbox.CountAsync());

        Assert.Equal(3550m, (await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol == "PETR4")).Amount);
    }

    [Fact]
    public async Task FailureBeforeFinancialSaveRollsBackAllThreeChanges()
    {
        await fixture.ResetAsync();
        var options = new DbContextOptionsBuilder<AccumulatorDb>().UseNpgsql(fixture.ConnectionString).AddInterceptors(new FailSave()).Options;

        await using (var db = new AccumulatorDb(options))
            await Assert.ThrowsAsync<IOException>(() => new ProcessOrder(new PostgresAccumulatorUnitOfWork(db), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("test", Request()));

        await using var check = fixture.Accumulator();

        Assert.Empty(await check.Orders.ToListAsync()); Assert.Empty(await check.Outbox.ToListAsync());

        Assert.All(await check.Exposures.ToListAsync(), x => Assert.Equal(0, x.Amount));
    }

    [Fact]
    public async Task GeneratorSubmissionAndFixOutboxRollBackTogether()
    {
        await fixture.ResetAsync();

        var options = new DbContextOptionsBuilder<GeneratorDb>().UseNpgsql(fixture.ConnectionString).AddInterceptors(new FailSave()).Options;

        await using (var db = new GeneratorDb(options))
            await Assert.ThrowsAsync<IOException>(() => new PostgresSubmissionStore(db).CreateAsync(Request(), default));

        await using var check = fixture.Generator();

        Assert.Empty(await check.Submissions.ToListAsync()); Assert.Empty(await check.FixOutbox.ToListAsync());
    }

    [Fact]
    public async Task FailureAfterSqlSaveBeforeCommitRollsBackDecisionExposureAndOutbox()
    {
        await fixture.ResetAsync();

        var options = new DbContextOptionsBuilder<AccumulatorDb>().UseNpgsql(fixture.ConnectionString).AddInterceptors(new FailCommit()).Options;

        await using (var db = new AccumulatorDb(options))
            await Assert.ThrowsAsync<IOException>(() => new ProcessOrder(new PostgresAccumulatorUnitOfWork(db), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("test", Request()));

        await using var check = fixture.Accumulator();

        Assert.Empty(await check.Orders.ToListAsync()); Assert.Empty(await check.Outbox.ToListAsync());

        Assert.All(await check.Exposures.ToListAsync(), x => Assert.Equal(0, x.Amount));
    }

    [Fact]
    public async Task FailureDuringFixConfirmationCommitLeavesSubmissionAndFixOutboxPending()
    {
        await fixture.ResetAsync();

        var request = Request();

        await using (var db = fixture.Generator()) await new PostgresSubmissionStore(db).CreateAsync(request, default);

        var decision = await Decide(request);

        var options = new DbContextOptionsBuilder<GeneratorDb>().UseNpgsql(fixture.ConnectionString).AddInterceptors(new FailCommit()).Options;

        await using (var db = new GeneratorDb(options))
            await Assert.ThrowsAsync<IOException>(() => new PostgresSubmissionStore(db).RecordAsync(decision, default));

        await using (var check = fixture.Generator())
        {
            Assert.Equal("Pending", (await check.Submissions.SingleAsync()).Status);

            Assert.Null((await check.FixOutbox.SingleAsync()).CompletedAt);
        }

        await using (var retry = fixture.Generator()) await new PostgresSubmissionStore(retry).RecordAsync(decision, default);

        await using var final = fixture.Generator();

        Assert.Equal("New", (await final.Submissions.SingleAsync()).Status);

        Assert.NotNull((await final.FixOutbox.SingleAsync()).CompletedAt);
    }

    [Fact]
    public async Task KafkaFailureAndAmbiguousAckDoNotUndoFinancialDecision()
    {
        await fixture.ResetAsync(); 
        
        var request = Request(); 
        
        await Decide(request);

        var publisher = new RecordingPublisher { FailAfterDelivery = true };

        await using (var db = fixture.Accumulator())
        {
            Assert.Equal(0, await new OutboxPump(db, publisher, NullLogger<OutboxPump>.Instance).PublishOnceAsync(default));

            Assert.Null((await db.Outbox.SingleAsync()).PublishedAt);

            Assert.Equal(OrderStatus.New, (await db.Orders.SingleAsync()).Status);

            await db.Outbox.ExecuteUpdateAsync(u => u.SetProperty(x => x.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)));
        }

        publisher.FailAfterDelivery = false;

        await using (var db = fixture.Accumulator())
            Assert.Equal(1, await new OutboxPump(db, publisher, NullLogger<OutboxPump>.Instance).PublishOnceAsync(default));

        Assert.Equal(2, publisher.Payloads.Count); Assert.Equal(publisher.Payloads[0], publisher.Payloads[1]);

        await using var check = fixture.Accumulator();
        
        Assert.Equal(3550m, (await check.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol == "PETR4")).Amount);
    }

    [Fact]
    public async Task FailedHeadBlocksOnlyItsOwnSymbol()
    {
        await fixture.ResetAsync(); await Decide(Request()); await Decide(Request()); await Decide(Request("VALE3"));

        await using var db = fixture.Accumulator();

        var head = await db.Outbox.Where(x => x.PartitionKey == "CLIENTE-001:PETR4").OrderBy(x => x.SymbolSequence).FirstAsync();

        head.NextAttemptAt = DateTime.UtcNow.AddHours(1); 
        
        await db.SaveChangesAsync();

        var publisher = new RecordingPublisher();

        await new OutboxPump(db, publisher, NullLogger<OutboxPump>.Instance).PublishOnceAsync(default);

        Assert.Single(publisher.Payloads); Assert.Contains("VALE3", publisher.Payloads[0]);

        Assert.Equal(2, await db.Outbox.CountAsync(x => x.PartitionKey == "CLIENTE-001:PETR4" && x.PublishedAt == null));
    }

    [Fact]
    public async Task InboxCommitWithoutOffsetCommitCanBeReplayedWithoutDuplicatingAuditOrDecidingSubmission()
    {
        await fixture.ResetAsync();
        
        var request = Request(); 
        
        await Decide(request);

        await using var a = fixture.Accumulator(); 
        
        var payload = (await a.Outbox.SingleAsync()).Payload;

        await using (var g = fixture.Generator()) await new PostgresSubmissionStore(g).CreateAsync(request, default);

        for (var i = 0; i < 2; i++)
        {
            await using var g = fixture.Generator();

            await new AuditInbox(g, NullLogger<AuditInbox>.Instance).AcceptAsync(payload, default);
        }

        await using var check = fixture.Generator();

        Assert.Equal(1, await check.Inbox.CountAsync()); Assert.Equal(1, await check.Events.CountAsync(x => x.Source == "Kafka"));

        Assert.Equal("Pending", (await check.Submissions.SingleAsync()).Status);
    }

    [Fact]
    public async Task InboxAndProjectionRollBackTogetherBeforeCommit()
    {
        await fixture.ResetAsync();
        
        await Decide(Request());

        await using var a = fixture.Accumulator();
        
        var payload = (await a.Outbox.SingleAsync()).Payload;

        var options = new DbContextOptionsBuilder<GeneratorDb>().UseNpgsql(fixture.ConnectionString).AddInterceptors(new FailSave()).Options;

        await using (var g = new GeneratorDb(options))
            await Assert.ThrowsAsync<IOException>(() => new AuditInbox(g, NullLogger<AuditInbox>.Instance).AcceptAsync(payload, default));

        await using var check = fixture.Generator(); Assert.Empty(await check.Inbox.ToListAsync()); Assert.Empty(await check.Events.ToListAsync());
    }

    [Fact]
    public async Task PoisonMessageIsPreservedIdempotentlyBeforeOffsetMayAdvance()
    {
        await fixture.ResetAsync();
        
        await using var db = fixture.Generator();

        var inbox = new AuditInbox(db, NullLogger<AuditInbox>.Instance);

        await Assert.ThrowsAsync<PoisonMessageException>(() => inbox.AcceptAsync("broken", default));

        await inbox.QuarantineAsync("test", 0, 1, "broken", "invalid", default);

        await inbox.QuarantineAsync("test", 0, 1, "broken", "invalid", default);

        Assert.Equal(1, await db.Quarantine.CountAsync()); Assert.Empty(await db.Inbox.ToListAsync());
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<string> Payloads { get; } = [];

        public bool FailAfterDelivery { get; set; }

        public Task PublishAsync(string key, string payload, CancellationToken ct)
        {
            Payloads.Add(payload);

            if (FailAfterDelivery) throw new IOException("Simulated process loss after delivery before marking outbox");

            return Task.CompletedTask;
        }
    }

    private sealed class FailSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new IOException("Injected failure before transaction commit");
    }

    private sealed class FailCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new IOException("Injected failure after SQL writes before commit");
    }
}
