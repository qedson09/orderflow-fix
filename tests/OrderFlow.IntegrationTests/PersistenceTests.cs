using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence.Orders;

namespace OrderFlow.IntegrationTests;

[Collection("Postgres")]
public sealed class PersistenceTests(PostgresFixture fixture)
{
    private static OrderRequest Request(string symbol = "PETR4", decimal qty = 100m, decimal price = 35m, OrderSide side = OrderSide.Buy) =>
        new(Guid.NewGuid().ToString("D"), symbol, side, qty, price);

    private async Task<OrderDecision> Process(OrderRequest request)
    {
        await using var db = fixture.Accumulator();

        return await new ProcessOrder(new PostgresAccumulatorUnitOfWork(db), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider()).ProcessAsync("test", request);
    }

    [Fact]
    public async Task MigrationsAreIdempotentAndSeedThreeSymbols()
    {
        await using var db = fixture.Accumulator(); await db.Database.MigrateAsync(); await db.Database.MigrateAsync();

        Assert.Equal(6, await db.Exposures.CountAsync());

        Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());

        Assert.False(db.Database.HasPendingModelChanges());

        await using var generator = fixture.Generator(); Assert.False(generator.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task AcceptedOrderAndExposureSurviveNewContext()
    {
        await fixture.ResetAsync(); 
        
        var result = await Process(Request());

        await using var db = fixture.Accumulator();

        Assert.Equal(OrderStatus.New, result.Status); Assert.Equal(3500, (await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol == "PETR4")).Amount);

        Assert.Equal(result.ExecId, (await db.Orders.SingleAsync()).ExecId);
    }

    [Fact]
    public async Task ConcurrentDuplicateAppliesOnlyOnceAndReturnsSameDecision()
    {
        await fixture.ResetAsync(); var request = Request();

        var results = await Task.WhenAll(Enumerable.Range(0,10).Select(_ => Process(request)));

        Assert.Single(results.Select(x => x.ExecId).Distinct());

        await using var db = fixture.Accumulator(); Assert.Single(await db.Orders.ToListAsync());

        Assert.Equal(3500, (await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol == "PETR4")).Amount);
    }

    [Fact]
    public async Task ConcurrentOrdersCannotOverrunLimit()
    {
        await fixture.ResetAsync();

        var results = await Task.WhenAll(Enumerable.Range(0,4).Select(_ => Process(Request(qty:60000,price:999))));

        Assert.Single(results, x => x.Status == OrderStatus.New); Assert.Equal(3, results.Count(x => x.Status == OrderStatus.Rejected));

        await using var db = fixture.Accumulator(); Assert.Equal(59940000, (await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol =="PETR4")).Amount);

        Assert.Equal(4, await db.Orders.CountAsync());
    }

    [Fact]
    public async Task RejectionIsDurableAndDoesNotMutateExposure()
    {
        await fixture.ResetAsync(); await Process(Request(qty:99999,price:999.99m,side:OrderSide.Sell));

        var request = Request(qty:10,price:999,side:OrderSide.Sell); var first = await Process(request); var second = await Process(request);

        Assert.Equal(OrderStatus.Rejected,first.Status); Assert.Equal(first,second);

        await using var db = fixture.Accumulator(); Assert.Equal(-99998000.01m,(await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol =="PETR4")).Amount);
    }

    [Fact]
    public async Task SeparateSymbolsProgressIndependently()
    {
        await fixture.ResetAsync();

        var results = await Task.WhenAll(OrderRules.Symbols.Select(s=>Process(Request(s,99999,999))));

        Assert.All(results, x=>Assert.Equal(OrderStatus.New,x.Status));

        await using var db = fixture.Accumulator(); Assert.All(await db.Exposures.Where(x => x.AccountId == "CLIENTE-001").ToListAsync(),x=>Assert.Equal(99899001,x.Amount));
    }

    [Fact]
    public async Task ConflictingReuseCannotChangeOriginalOrder()
    {
        await fixture.ResetAsync(); var request = Request(); var original = await Process(request);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => Process(request with { Symbol="VALE3" }));

        Assert.Equal(original,await Process(request));

        await using var db = fixture.Accumulator(); Assert.Equal(0,(await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol =="VALE3")).Amount);
    }

    [Fact]
    public async Task InvalidFixBusinessDataIsPersistedAsRejected()
    {
        await fixture.ResetAsync(); var result = await Process(Request(qty:1.5m));

        Assert.Equal(OrderStatus.Rejected,result.Status);

        await using var db = fixture.Accumulator(); Assert.Equal(0,(await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol =="PETR4")).Amount);
    }

    [Fact]
    public async Task GeneratorPreservesPendingAndTerminalStatesAcrossRestart()
    {
        await fixture.ResetAsync(); var request = Request();

        await using(var db = fixture.Generator())
        {
            var store = new PostgresSubmissionStore(db);

            Assert.Equal("Pending",(await store.CreateAsync(request,default)).Status);

            Assert.Equal(request.ClOrdId,(await store.CreateAsync(request,default)).ClOrdId);

            await Assert.ThrowsAsync<IdempotencyConflictException>(()=>store.CreateAsync(request with { Price=99 },default));
        }

        var decision = await Process(request);

        await using(var db = fixture.Generator()) await new PostgresSubmissionStore(db).RecordAsync(decision,default);

        await using(var db = fixture.Generator())
        {
            var store = new PostgresSubmissionStore(db); await store.RecordAsync(decision,default);

            Assert.Equal("New",(await store.FindAsync(request.ClOrdId,default))!.Status);

            Assert.Empty(await store.PendingAsync(default));
        }
    }
}
