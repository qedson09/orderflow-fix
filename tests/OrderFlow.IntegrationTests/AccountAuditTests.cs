using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Persistence.Orders;
using OrderFlow.Infrastructure.Persistence.Configuration;

namespace OrderFlow.IntegrationTests;

[Collection("Postgres")]
public class AccountAuditTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SummaryExcludesUnusedAndRejectedAssetsButKeepsZeroAfterOffset()
    {
        await fixture.ResetAsync();

        async Task Apply(OrderRequest request)
        {
            await using var db = fixture.Accumulator();

            await new ProcessOrder(new PostgresAccumulatorUnitOfWork(db), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("test", request);
        }

        await using (var fresh = fixture.Accumulator())
            Assert.Empty(await fresh.Exposures.TradedBy("CLIENTE-001").ToListAsync());

        var buy = new OrderRequest(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 100, 35);

        await Apply(buy);

        await Apply(buy with { ClOrdId = Guid.NewGuid().ToString("D"), Side = OrderSide.Sell });

        await Apply(buy with { ClOrdId = Guid.NewGuid().ToString("D"), Symbol = "VIIA4", Quantity = 0 });

        await Apply(buy with { ClOrdId = Guid.NewGuid().ToString("D"), Symbol = "VALE3", AccountId = "CLIENTE-002" });

        await using var check = fixture.Accumulator();

        var summary = await check.Exposures.TradedBy("CLIENTE-001").ToListAsync();

        Assert.Single(summary); Assert.Equal("PETR4", summary[0].Symbol); Assert.Equal(0, summary[0].Amount);
    }

    [Fact]
    public async Task ConcurrentAccountsHaveIndependentLimits()
    {
        await fixture.ResetAsync();

        async Task<OrderDecision> Process(string account)
        {
            await using var db = fixture.Accumulator();

            return await new ProcessOrder(new PostgresAccumulatorUnitOfWork(db), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("test",
                new(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 99999, 999.99m, account));
        }

        var decisions = await Task.WhenAll(Process("CLIENTE-001"), Process("CLIENTE-002"));

        Assert.All(decisions, d => Assert.Equal(OrderStatus.New, d.Status));

        await using var check = fixture.Accumulator();

        Assert.All(await check.Exposures.Where(x => x.Symbol == "PETR4").ToListAsync(), e => Assert.Equal(99998000.01m, e.Amount));

        Assert.Equal(2, await check.Outbox.Select(x => x.PartitionKey).Distinct().CountAsync());
    }

    [Fact]
    public async Task LifecycleAndKafkaHistorySurviveReplayWithoutDuplicatingDecision()
    {
        await fixture.ResetAsync();

        var request = new OrderRequest(Guid.NewGuid().ToString("D"), "PETR4", OrderSide.Buy, 100, 35, "CLIENTE-002");

        await using var generator = fixture.Generator();

        var submissions = new PostgresSubmissionStore(generator);

        await submissions.CreateAsync(request, default);

        await submissions.CreateAsync(request, default);

        await submissions.MarkAttemptAsync(request.ClOrdId, default);

        await submissions.MarkAttemptAsync(request.ClOrdId, default);

        await using var accumulator = fixture.Accumulator();

        var decision = await new ProcessOrder(new PostgresAccumulatorUnitOfWork(accumulator), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("test", request);

        await submissions.RecordAsync(decision, default);

        await submissions.RecordAsync(decision, default);

        var inbox = new AuditInbox(generator, NullLogger<AuditInbox>.Instance);

        var payload = (await accumulator.Outbox.SingleAsync()).Payload;

        await inbox.AcceptAsync(payload, default); await inbox.AcceptAsync(payload, default);

        var events = await generator.Events.Where(x => x.ClOrdId == request.ClOrdId).ToListAsync();

        Assert.Equal(5, events.Count);

        Assert.Single(events, x => x.Type == "SubmissionPersisted");

        Assert.Equal(2, events.Count(x => x.Type == "FixDispatchAttempt"));

        Assert.Single(events, x => x.Type == "ExecutionReportPersisted");

        Assert.Single(events, x => x.Source == "Kafka");
    }
}
