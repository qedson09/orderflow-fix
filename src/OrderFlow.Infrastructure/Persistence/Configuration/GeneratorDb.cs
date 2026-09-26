using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence.Auditing;
using OrderFlow.Infrastructure.Persistence.Orders;

namespace OrderFlow.Infrastructure.Persistence;

public sealed class GeneratorDb(DbContextOptions<GeneratorDb> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<FixOutbox> FixOutbox => Set<FixOutbox>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<OrderEventProjection> Events => Set<OrderEventProjection>();
    public DbSet<QuarantinedMessage> Quarantine => Set<QuarantinedMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("generator");

        var item = model.Entity<Submission>(); item.ToTable("Submissions", t => t.HasCheckConstraint("CK_Submissions_Side", "\"Side\" IN ('B', 'S')")); item.HasKey(x => x.ClOrdId);

        item.Property(x => x.AccountId).HasDefaultValue("CLIENTE-001");

        item.HasIndex(x => new { x.AccountId, x.CreatedAt });

        item.Property(x => x.Side).HasConversion(x => x.ToCode(), x => OrderSideCode.Parse(x)).HasMaxLength(1);

        item.Property(x => x.Price).HasPrecision(18, 2);

        item.Property(x => x.Quantity).HasPrecision(10, 0);

        item.HasIndex(x => new { x.Status, x.LastAttemptAt });

        var fix = model.Entity<FixOutbox>(); fix.ToTable("FixOutbox"); fix.HasKey(x => x.ClOrdId);

        fix.HasOne<Submission>().WithOne().HasForeignKey<FixOutbox>(x => x.ClOrdId);

        fix.HasIndex(x => new { x.CompletedAt, x.NextAttemptAt });

        var inbox = model.Entity<InboxMessage>(); inbox.ToTable("InboxMessages"); inbox.HasKey(x => new { x.Consumer, x.EventId });

        var projection = model.Entity<OrderEventProjection>(); projection.ToTable("OrderEventProjections"); projection.HasKey(x => x.EventId);

        projection.Property(x => x.Source).HasDefaultValue("Kafka");

        projection.HasIndex(x => new { x.ClOrdId, x.OccurredAt });

        var quarantine = model.Entity<QuarantinedMessage>(); quarantine.ToTable("QuarantinedMessages");

        quarantine.HasKey(x => new { x.Topic, x.Partition, x.Offset });
    }
}
