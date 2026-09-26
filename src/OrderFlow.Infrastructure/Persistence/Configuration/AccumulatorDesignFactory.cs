using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Infrastructure.Persistence.Configuration;

public sealed class AccumulatorDesignFactory : IDesignTimeDbContextFactory<AccumulatorDb>
{
    public AccumulatorDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AccumulatorDb>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Trading") ?? "Host=localhost;Database=orderflow;Username=orderflow;Password=local-only",
            n => n.MigrationsHistoryTable("__EFMigrationsHistory", "accumulator")).Options);
}
