using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Exposures;
using OrderFlow.Domain.Customers;

namespace OrderGenerator.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("read")]
    public IResult List()
    {
        return Results.Ok(CustomerCatalog.All);
    }

    [HttpGet("{accountId}/exposures")]
    [EnableRateLimiting("read")]
    [ProducesResponseType(typeof(ExposureResponse), 200)]
    public async Task<IResult> Exposures([FromRoute] string accountId, [FromServices] IHttpClientFactory clients, CancellationToken ct, [FromServices] ILogger<AccountsController> logger)
    {
        if (!CustomerCatalog.Contains(accountId))
            return Results.Problem(statusCode: 404, title: "Cliente não encontrado");

        try
        {
            var summary = await clients.CreateClient("exposures").GetFromJsonAsync<ExposureResponse>(
                $"/internal/accounts/{Uri.EscapeDataString(accountId)}/exposures", ct);

            return summary is null
                ? Results.Problem(statusCode: 503, title: "Exposição indisponível")
                : Results.Ok(summary);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Falha ao consultar exposição {AccountId}", accountId);

            return Results.Problem(statusCode: 503, title: "Exposição temporariamente indisponível");
        }
    }
}
