using Microsoft.AspNetCore.Mvc;
using OrderFlow.Infrastructure.Fix;
using OrderFlow.Infrastructure.Persistence.Configuration;

namespace OrderAccumulator.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("live")]
    public IResult Live()
    {
        return Results.Ok(new { status = "live" });
    }

    [HttpGet("ready")]
    public async Task<IResult> Ready([FromServices] AccumulatorDb db, [FromServices] AcceptorService fix, CancellationToken ct)
    {
        return fix.Started && await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(503);
    }
}
