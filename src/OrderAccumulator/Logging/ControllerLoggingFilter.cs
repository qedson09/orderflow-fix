using Microsoft.AspNetCore.Mvc.Filters;

namespace OrderAccumulator.Logging;

public sealed class ControllerLoggingFilter(ILogger<ControllerLoggingFilter> logger) : IAsyncResourceFilter
{

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = context.HttpContext.TraceIdentifier,
            ["Controller"] = context.ActionDescriptor.RouteValues["controller"],
            ["Action"] = context.ActionDescriptor.RouteValues["action"],
            ["ClOrdId"] = request.RouteValues["clOrdId"],
            ["AccountId"] = request.RouteValues["accountId"]
        });

        try
        {
            var result = await next();

            if (result.Exception is not null && !result.ExceptionHandled)
            {
                logger.LogError(result.Exception, "Erro na chamada {Method} {Path}", request.Method, request.Path);

                return;
            }

            var status = context.HttpContext.Response.StatusCode;

            if (status >= 500) 
                logger.LogError("Chamada {Method} {Path} retornou {StatusCode}", request.Method, request.Path, status);
            else if (status >= 400) 
                logger.LogWarning("Chamada {Method} {Path} retornou {StatusCode}", request.Method, request.Path, status);
            else 
                logger.LogInformation("Chamada {Method} {Path} concluída com {StatusCode}", request.Method, request.Path, status);
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Chamada cancelada pelo cliente {Method} {Path}", request.Method, request.Path);

            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro na chamada {Method} {Path}", request.Method, request.Path);

            throw;
        }
    }
}
