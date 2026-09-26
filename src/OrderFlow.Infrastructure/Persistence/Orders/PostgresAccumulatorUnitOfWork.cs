using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence.Auditing;
using OrderFlow.Infrastructure.Persistence.Configuration;

namespace OrderFlow.Infrastructure.Persistence.Orders;

public sealed class PostgresAccumulatorUnitOfWork(AccumulatorDb db) : IAccumulatorUnitOfWork
{
    public async Task<IOrderTransaction> BeginAsync(string session, string clOrdId, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // Duplicate IDs with different symbols also serialize; hash collisions only reduce concurrency.
            var key = session + "|" + clOrdId;

            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

            return new PostgresOrderTransaction(tx);
        }
        catch { await tx.DisposeAsync(); throw; }
    }

    public Task<Order?> FindOrderAsync(string session,string clOrdId,CancellationToken ct) =>
        db.Orders.SingleOrDefaultAsync(x => x.SessionKey == session && x.ClOrdId == clOrdId,ct);

    public Task<SymbolExposure> LockExposureAsync(string symbol,CancellationToken ct, string accountId = "CLIENTE-001") => db.Exposures.FromSqlInterpolated(
        $"SELECT * FROM accumulator.\"Exposures\" WHERE \"AccountId\" = {accountId} AND \"Symbol\" = {symbol} FOR UPDATE").SingleAsync(ct);

    public void Add(Order order) => db.Orders.Add(order);

    public void AddEvent(OrderDecisionEvent message) => db.Outbox.Add(OutboxMessage.From(message));

    public async Task SaveAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);
}
