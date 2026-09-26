using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Configuration;
using Testcontainers.PostgreSql;

namespace OrderFlow.IntegrationTests;

[CollectionDefinition("Postgres", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = "";

    public AccumulatorDb Accumulator() => new(new DbContextOptionsBuilder<AccumulatorDb>().UseNpgsql(ConnectionString,
        n => n.MigrationsHistoryTable("__EFMigrationsHistory", "accumulator")).Options);

    public GeneratorDb Generator() => new(new DbContextOptionsBuilder<GeneratorDb>().UseNpgsql(ConnectionString,
        n => n.MigrationsHistoryTable("__EFMigrationsHistory", "generator")).Options);

    public async Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES") ?? "";

        if (ConnectionString.Length == 0)
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

            await _container.StartAsync(); ConnectionString = _container.GetConnectionString();
        }

        await using var a = Accumulator(); await a.Database.MigrateAsync();

        await using var g = Generator(); await g.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var a = Accumulator();

        await a.Outbox.ExecuteDeleteAsync();

        await a.Orders.ExecuteDeleteAsync();

        await a.Database.ExecuteSqlRawAsync("UPDATE accumulator.\"Exposures\" SET \"Amount\" = 0, \"Version\" = 0, \"DecisionSequence\" = 0");

        await using var g = Generator();

        await g.FixOutbox.ExecuteDeleteAsync(); await g.Events.ExecuteDeleteAsync();

        await g.Inbox.ExecuteDeleteAsync(); await g.Quarantine.ExecuteDeleteAsync();

        await g.Submissions.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync() 
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
