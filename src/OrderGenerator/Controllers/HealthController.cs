using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Persistence;

namespace OrderGenerator.Controllers;

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
    public async Task<IResult> Ready([FromServices] GeneratorDb db, [FromServices] IOrderTransport fix, [FromServices] AuditHealth audit, CancellationToken ct)
    {
        var database = await db.Database.CanConnectAsync(ct);

        return Results.Json(new { database, fixConnected = fix.IsLoggedOn, auditAvailable = audit.Available }, statusCode: database && fix.IsLoggedOn ? 200 : 503);
    }
}
