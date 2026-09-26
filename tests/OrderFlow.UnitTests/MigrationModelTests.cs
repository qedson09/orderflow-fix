using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderFlow.Infrastructure.Persistence.Configuration;

namespace OrderFlow.UnitTests;

public class MigrationModelTests
{
    [Fact]
    public void SnapshotsMatchBothModelsAndUpgradePreservesExistingExposure()
    {
        using var accumulator = new AccumulatorDesignFactory().CreateDbContext([]);

        using var generator = new GeneratorDesignFactory().CreateDbContext([]);

        Assert.False(accumulator.Database.HasPendingModelChanges());

        Assert.False(generator.Database.HasPendingModelChanges());

        var financialSql = accumulator.GetService<IMigrator>().GenerateScript("20260922001119_DurableOutbox");

        Assert.Contains("PRIMARY KEY (\"AccountId\", \"Symbol\")", financialSql);

        Assert.DoesNotContain("DELETE FROM accumulator.\"Exposures\"", financialSql);

        Assert.DoesNotContain("UPDATE accumulator.\"Exposures\"", financialSql);

        var auditSql = generator.GetService<IMigrator>().GenerateScript("20260922001309_DurableFixAndAudit");

        Assert.Contains("SubmissionPersisted", auditSql);

        Assert.Contains("ExecutionReportPersisted", auditSql);
    }
}
