using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Customers;
using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence.Auditing;

namespace OrderFlow.Infrastructure.Persistence.Configuration;

public sealed class AccumulatorDb(DbContextOptions<AccumulatorDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<SymbolExposure> Exposures => Set<SymbolExposure>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("accumulator");

        var customer = model.Entity<Customer>(); customer.ToTable("Customers"); customer.HasKey(x => x.Id);

        customer.HasData(CustomerCatalog.All);

        var order = model.Entity<Order>();

        order.ToTable("Orders", t => t.HasCheckConstraint("CK_Orders_Side", "\"Side\" IN ('B', 'S')")); order.HasKey(x => x.Id);

        order.HasIndex(x => new { x.SessionKey, x.ClOrdId }).IsUnique();

        order.Property(x => x.Side).HasConversion(x => x.ToCode(), x => OrderSideCode.Parse(x)).HasMaxLength(1);

        order.Property(x => x.Status).HasConversion<string>();

        var exposure = model.Entity<SymbolExposure>();

        exposure.ToTable("Exposures", t => t.HasCheckConstraint("CK_Exposure_Limit", "abs(\"Amount\") <= 100000000"));

        exposure.HasKey(x => new { x.AccountId, x.Symbol });

        exposure.HasOne<Customer>().WithMany().HasForeignKey(x => x.AccountId);

        order.Property(x => x.AccountId).HasDefaultValue("CLIENTE-001");

        exposure.Property(x => x.AccountId).HasDefaultValue("CLIENTE-001");

        exposure.Property(x => x.Amount).HasPrecision(18, 2);

        exposure.Property(x => x.Version).IsConcurrencyToken();

        exposure.HasData(CustomerCatalog.All.SelectMany(c => OrderRules.Symbols.Select(s => new { AccountId = c.Id, Symbol = s, Amount = 0m, Version = 0L, DecisionSequence = 0L })));

        var message = model.Entity<OutboxMessage>();

        message.ToTable("OutboxMessages"); message.HasKey(x => x.EventId);

        message.Property(x => x.Payload).HasColumnType("jsonb");

        message.HasIndex(x => new { x.PartitionKey, x.SymbolSequence }).IsUnique();

        message.HasIndex(x => new { x.PublishedAt, x.NextAttemptAt });
    }
}
