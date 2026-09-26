using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Exposures;
using OrderFlow.Domain.Customers;
using OrderFlow.Infrastructure.Persistence.Configuration;

namespace OrderAccumulator.Controllers;

[ApiController]
[Route("internal/accounts")]
public sealed class AccountsController : ControllerBase
{
    [HttpGet("{accountId}/exposures")]
    [ProducesResponseType(typeof(ExposureResponse), 200)]
    public async Task<IResult> Exposures([FromRoute] string accountId, [FromServices] AccumulatorDb db, [FromServices] IExposureLimitProvider limits, CancellationToken ct)
    {
        if (!CustomerCatalog.Contains(accountId)) return Results.NotFound();

        var rows = await db.Exposures.AsNoTracking().TradedBy(accountId).ToListAsync(ct);

        var summaries = new List<ExposureSummary>();

        foreach (var row in rows)
        {
            var limit = await limits.GetLimitAsync(row.AccountId, row.Symbol, ct);
            summaries.Add(ExposureSummary.From(row, limit));
        }

        return Results.Ok(new ExposureResponse(accountId, DateTime.UtcNow, summaries));
    }
}
