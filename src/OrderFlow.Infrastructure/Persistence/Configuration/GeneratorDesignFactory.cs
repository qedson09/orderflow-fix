using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Infrastructure.Persistence.Configuration;

public sealed class GeneratorDesignFactory : IDesignTimeDbContextFactory<GeneratorDb>
{
    public GeneratorDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<GeneratorDb>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Trading") ?? "Host=localhost;Database=orderflow;Username=orderflow;Password=local-only",
            n => n.MigrationsHistoryTable("__EFMigrationsHistory", "generator")).Options);
}
