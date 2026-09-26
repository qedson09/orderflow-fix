using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence.Auditing;

namespace OrderFlow.Infrastructure.Persistence.Orders;

public sealed class PostgresSubmissionStore(GeneratorDb db) : ISubmissionStore
{
    public async Task<SubmissionView> CreateAsync(OrderRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var key = "generator|" + request.ClOrdId;

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

        var previous = await db.Submissions.FindAsync([request.ClOrdId], ct);

        if (previous is not null)
        {
            if (!previous.Matches(request)) throw new IdempotencyConflictException();

            await tx.CommitAsync(ct);

            return previous.View();
        }

        var row = new Submission { ClOrdId = request.ClOrdId, Symbol = request.Symbol, Side = request.Side,
            Quantity = request.Quantity, Price = request.Price, AccountId = request.AccountId };

        db.Submissions.Add(row);

        db.FixOutbox.Add(new FixOutbox { ClOrdId = request.ClOrdId });

        db.Events.Add(Lifecycle(row, "SubmissionPersisted", "Solicitação e pendência de envio FIX persistidas.", row.CreatedAt));

        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return row.View();
    }

    public async Task<SubmissionView?> FindAsync(string id, CancellationToken ct) =>
        (await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.ClOrdId == id, ct))?.View();

    public async Task<IReadOnlyList<SubmissionView>> RecentAsync(CancellationToken ct) =>
        (await db.Submissions.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct)).Select(x => x.View()).ToList();

    public async Task<IReadOnlyList<SubmissionView>> PendingAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var pendingIds = db.FixOutbox.Where(x => x.CompletedAt == null && x.NextAttemptAt <= now).Select(x => x.ClOrdId);

        return (await db.Submissions.AsNoTracking().Where(x => x.Status == "Pending" && pendingIds.Contains(x.ClOrdId)).OrderBy(x => x.LastAttemptAt).ThenBy(x => x.CreatedAt)
            .Take(100).ToListAsync(ct)).Select(x => x.View()).ToList();
    }
    public async Task MarkAttemptAsync(string id, CancellationToken cancellationToken)
    {
        var entry = await db.FixOutbox.SingleAsync(x => x.ClOrdId == id, cancellationToken);

        if (entry.CompletedAt is not null) return;

        entry.Attempts++;

        entry.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(30, Math.Pow(2, Math.Min(entry.Attempts, 5))) + Random.Shared.NextDouble());

        var row = await db.Submissions.SingleAsync(x => x.ClOrdId == id, cancellationToken);

        db.Events.Add(Lifecycle(row, "FixDispatchAttempt", $"Tentativa de envio FIX nº {entry.Attempts} iniciada; não confirma entrega.", DateTime.UtcNow));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordAsync(OrderDecision decision, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var key = "generator|" + decision.ClOrdId;

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

        var row = await db.Submissions.SingleOrDefaultAsync(x => x.ClOrdId == decision.ClOrdId, cancellationToken);

        if (row is null) throw new InvalidOperationException("ExecutionReport sem submissão correspondente.");

        if (!row.Matches(new(decision.ClOrdId, decision.Symbol, decision.Side, decision.Quantity, decision.Price, decision.AccountId)))
            throw new InvalidOperationException("ExecutionReport diverge da submissão original.");

        if (row.Status != "Pending")
        {
            if (row.ExecId != decision.ExecId || row.Status != decision.Status.ToString() || row.OrderId != decision.Id.ToString("N"))
                throw new InvalidOperationException("ExecutionReport conflitante.");

            await tx.CommitAsync(cancellationToken);

            return;
        }

        row.Status = decision.Status.ToString(); row.ExecId = decision.ExecId; row.OrderId = decision.Id.ToString("N");

        row.Reason = decision.Reason; row.UpdatedAt = DateTime.UtcNow;

        var pending = await db.FixOutbox.SingleAsync(x => x.ClOrdId == row.ClOrdId, cancellationToken);

        pending.CompletedAt = row.UpdatedAt;

        db.Events.Add(Lifecycle(row, "ExecutionReportPersisted", $"ExecutionReport FIX persistido: {row.Status}. {row.Reason}", row.UpdatedAt.Value));

        await db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    private static OrderEventProjection Lifecycle(Submission row, string type, string description, DateTime occurredAt) => new()
    {
        EventId = Guid.NewGuid(), ClOrdId = row.ClOrdId, Symbol = row.Symbol, Type = type,
        Description = description, OccurredAt = occurredAt, Source = "Generator"
    };
}
