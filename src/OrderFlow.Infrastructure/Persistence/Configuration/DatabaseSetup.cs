using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OrderFlow.Infrastructure.Persistence.Configuration;

public static class DatabaseSetup
{
    public static IServiceCollection AddAccumulatorDatabase(this IServiceCollection services, string connection) =>
        services.AddDbContext<AccumulatorDb>(o => o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistory", "accumulator")));

    public static IServiceCollection AddGeneratorDatabase(this IServiceCollection services, string connection) =>
        services.AddDbContext<GeneratorDb>(o => o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistory", "generator")));

    public static async Task MigrateAsync<T>(IServiceProvider services, CancellationToken ct = default) where T : DbContext
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<T>();

        // Session-scoped advisory lock coordinates migrations across both hosts.
        await db.Database.OpenConnectionAsync(ct);

        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(440044)", ct);

            try { await db.Database.MigrateAsync(ct); }
            finally { await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(440044)", ct); }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
