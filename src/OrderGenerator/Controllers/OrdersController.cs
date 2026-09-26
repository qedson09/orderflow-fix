using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Customers;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Orders;

namespace OrderGenerator.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpGet("")]
    [EnableRateLimiting("read")]
    [ProducesResponseType(typeof(OrderPage), 200)]
    public async Task<IResult> List([FromQuery] string? accountId, [FromQuery] int? page, [FromQuery] int? pageSize, [FromServices] GeneratorDb db, CancellationToken ct)
    {
        if (accountId is not null && !CustomerCatalog.Contains(accountId)) return Results.Problem(statusCode: 404, title: "Cliente não encontrado");

        var requestedPage = page ?? 1;

        var size = pageSize ?? 10;

        if (requestedPage < 1 || size is < 1 or > 100)
            return Results.Problem(statusCode: 400, title: "Paginação inválida", detail: "Page deve ser positivo e pageSize deve estar entre 1 e 100.");

        return Results.Ok(await SubmissionQueries.ListPageAsync(db, accountId, requestedPage, size, ct));
    }

    [HttpGet("{clOrdId}")]
    [EnableRateLimiting("read")]
    public async Task<IResult> Get([FromRoute] string clOrdId, [FromServices] ISubmissionStore store, CancellationToken ct)
    { 
        return await store.FindAsync(clOrdId, ct) is { } row ? Results.Ok(ApiOrder.From(row)) : Results.Problem(statusCode: 404, title: "Ordem não encontrada"); 
    }

    [HttpGet("{clOrdId}/events")]
    [EnableRateLimiting("read")]
    public async Task<IResult> Events([FromRoute] string clOrdId, [FromServices] GeneratorDb db, [FromServices] AuditHealth audit, CancellationToken ct)
    {
        if (!await db.Submissions.AnyAsync(x => x.ClOrdId == clOrdId, ct)) return Results.NotFound();

        var events = await db.Events.AsNoTracking().Where(x => x.ClOrdId == clOrdId).OrderBy(x => x.OccurredAt).ThenBy(x => x.EventId)
            .Select(x => new { x.EventId, x.ClOrdId, x.OccurredAt, x.Type, x.Description, x.Source }).ToListAsync(ct);

        return Results.Ok(new { available = true, kafkaAvailable = audit.Available, events });
    }

    [HttpPost("")]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiOrder), 202)]
    [ProducesResponseType(typeof(ApiOrder), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IResult> Create([FromBody] OrderInput input, [FromServices] SubmitOrder useCase, [FromServices] ILogger<OrdersController> logger, CancellationToken ct)
    {
        try
        {
            var result = await useCase.ExecuteAsync(input.ToDomain(), ct);

            logger.LogInformation("Solicitação HTTP persistida {ClOrdId} {Status}", result.ClOrdId, result.Status);

            var response = ApiOrder.From(result);

            return result.Status == "Pending" ? Results.Accepted($"/api/orders/{result.ClOrdId}", response) : Results.Ok(response);
        }
        catch (OrderValidationException ex)
        {
            logger.LogWarning("Validação da ordem recusada {ClOrdId}: {Fields}", input.ClOrdId, string.Join(",", ex.Errors.Keys));

            return Results.ValidationProblem(ex.Errors, title: "Confira os dados da ordem", detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "ORDER_VALIDATION" });
        }
        catch (ArgumentException ex) 
        { 
            logger.LogWarning(ex, "Ordem inválida {ClOrdId}", input.ClOrdId); return Results.Problem(statusCode: 400, title: "Ordem inválida", detail: ex.Message);
        }
        catch (IdempotencyConflictException ex) 
        {
            logger.LogWarning(ex, "Conflito de identificador {ClOrdId}", input.ClOrdId); return Results.Problem(statusCode: 409, title: "Conflito de identificador", detail: ex.Message);
        }
    }
}
