using System.Data;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Orders;

namespace OrderFlow.Infrastructure.Persistence.Orders;

public static class SubmissionQueries
{
    public static async Task<OrderPage> ListPageAsync(GeneratorDb db, string? accountId,
        int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new ArgumentException("Page deve ser positivo e pageSize deve estar entre 1 e 100.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var query = db.Submissions.AsNoTracking().Where(x => accountId == null || x.AccountId == accountId);

        var total = await query.CountAsync(ct);

        var pages = total == 0 ? 0 : (total - 1) / pageSize + 1;

        var current = Math.Min(page, Math.Max(1, pages));

        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.ClOrdId)

            .Skip((current - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        await tx.CommitAsync(ct);

        return new(rows.Select(x => ApiOrder.From(x.View())).ToList(), current, pageSize, total, pages);
    }
}
