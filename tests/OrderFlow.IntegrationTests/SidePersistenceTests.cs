using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Persistence.Orders;

namespace OrderFlow.IntegrationTests;

[Collection("Postgres")]
public sealed class SidePersistenceTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("B")]
    [InlineData("S")]
    public async Task SideIsOneCharacterInBothSchemasAndSurvivesMigration(string code)
    {
        await fixture.ResetAsync();

        var input = new OrderInput(Guid.NewGuid().ToString("D"), "PETR4", code, 10, "35.50");

        var request = input.ToDomain();

        await using var generator = fixture.Generator();

        await using var accumulator = fixture.Accumulator();

        var submissions = new PostgresSubmissionStore(generator);

        await submissions.CreateAsync(request, default);

        var processor = new ProcessOrder(new PostgresAccumulatorUnitOfWork(accumulator), new OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider());

        var decision = await processor.ProcessAsync("test", request);

        await submissions.RecordAsync(decision, default);

        var exposure = await accumulator.Exposures.SingleAsync(x => x.AccountId == request.AccountId && x.Symbol == request.Symbol);

        var amount = exposure.Amount;

        await accumulator.GetService<IMigrator>().MigrateAsync("20260922204037_CustomerExposure");

        await generator.GetService<IMigrator>().MigrateAsync("20260922204056_CustomerAudit");

        await accumulator.Database.MigrateAsync();

        await generator.Database.MigrateAsync();

        Assert.Equal(code, await accumulator.Database.SqlQuery<string>(
            $"SELECT \"Side\" AS \"Value\" FROM accumulator.\"Orders\" WHERE \"ClOrdId\" = {request.ClOrdId}").SingleAsync());

        Assert.Equal(code, await generator.Database.SqlQuery<string>(
            $"SELECT \"Side\" AS \"Value\" FROM generator.\"Submissions\" WHERE \"ClOrdId\" = {request.ClOrdId}").SingleAsync());

        accumulator.ChangeTracker.Clear(); generator.ChangeTracker.Clear();

        Assert.Equal(request.Side, (await accumulator.Orders.SingleAsync()).Side);

        Assert.Equal(code, ApiOrder.From((await generator.Submissions.SingleAsync()).View()).Side);

        Assert.Equal(amount, (await accumulator.Exposures.SingleAsync(x => x.AccountId == request.AccountId && x.Symbol == request.Symbol)).Amount);

        Assert.Equal(decision.ExecId, (await processor.ProcessAsync("test", request)).ExecId);

        var invalid = await Assert.ThrowsAsync<PostgresException>(() => generator.Database.ExecuteSqlRawAsync(
            "UPDATE generator.\"Submissions\" SET \"Side\" = 'X'"));

        Assert.Equal("23514", invalid.SqlState);
    }
}
