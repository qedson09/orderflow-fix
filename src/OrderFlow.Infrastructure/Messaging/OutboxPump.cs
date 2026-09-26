using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Persistence.Configuration;
namespace OrderFlow.Infrastructure.Messaging;

public sealed class OutboxPump(AccumulatorDb db, IEventPublisher publisher, ILogger<OutboxPump> logger)
{
    public async Task<int> PublishOnceAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var locked = false;
        try
        {
            locked = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(440045) AS \"Value\"").SingleAsync(ct);

            if (!locked) return 0;

            // Apenas a cabeça de cada símbolo: nunca ultrapassar evento falho do mesmo símbolo.
            var heads = await db.Outbox.FromSqlRaw("""
                SELECT DISTINCT ON ("PartitionKey") * FROM accumulator."OutboxMessages"
                WHERE "PublishedAt" IS NULL
                ORDER BY "PartitionKey", "SymbolSequence" NULLS LAST, "CreatedAt", "EventId"
                """).AsNoTracking().ToListAsync(ct);

            var count = 0;

            foreach (var row in heads.Where(x => x.NextAttemptAt <= DateTime.UtcNow))
            {
                try
                {
                    await publisher.PublishAsync(row.PartitionKey, row.Payload, ct);

                    // Uma queda aqui repete o mesmo EventId; o consumidor deve deduplicar.
                    await db.Outbox.Where(x => x.EventId == row.EventId).ExecuteUpdateAsync(u => u
                        .SetProperty(x => x.PublishedAt, DateTime.UtcNow).SetProperty(x => x.LastError, (string?)null), ct);

                    logger.LogInformation("Evento publicado {EventId} símbolo {Symbol}", row.EventId, row.PartitionKey);

                    count++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) 
                { 
                    throw; 
                }
                catch (Exception ex)
                {
                    var attempt = row.Attempts + 1;

                    var next = DateTime.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, Math.Min(attempt, 6))) + Random.Shared.NextDouble());

                    await db.Outbox.Where(x => x.EventId == row.EventId).ExecuteUpdateAsync(u => u
                        .SetProperty(x => x.Attempts, attempt).SetProperty(x => x.NextAttemptAt, next)
                        .SetProperty(x => x.LastError, ex.GetType().Name), ct);

                    logger.LogWarning(ex, "Publicação pendente {EventId}; tentativa {Attempt}", row.EventId, attempt);
                }
            }

            return count;
        }
        finally
        {
            if (locked) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(440045)", CancellationToken.None);

            await db.Database.CloseConnectionAsync();
        }
    }
}
