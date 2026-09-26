using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
namespace OrderFlow.Infrastructure.Fix;

public sealed class GeneratorFixApplication(IServiceScopeFactory scopes, ILogger<GeneratorFixApplication> logger)
    : FixApplicationBase(logger), IOrderTransport
{
    public void OnMessage(BusinessMessageReject report, SessionID sessionID) =>
        logger.LogWarning("FIX BusinessMessageReject {Session}: {Text}; nenhuma rejeição financeira inferida", sessionID,
            report.IsSetField(Tags.Text) ? report.Text.Value : "Sem motivo");

    public bool Send(OrderRequest request, DateTime createdAt) => IsLoggedOn && SessionId is { } id &&
        Session.SendToTarget(FixMapper.ToMessage(request, createdAt), id);

    public void OnMessage(ExecutionReport report, SessionID sessionID)
    {
        try
        {
            var decision = FixMapper.FromMessage(report);

            using var scope = scopes.CreateScope();

            scope.ServiceProvider.GetRequiredService<ISubmissionStore>().RecordAsync(decision, CancellationToken.None).GetAwaiter().GetResult();

            logger.LogInformation("Resultado persistido {ClOrdId}: {Status}", decision.ClOrdId, decision.Status);
        }
        catch (Exception ex) 
        { 
            logger.LogError(ex, "ExecutionReport não persistido; ordem continua pendente para recuperação"); 
        }
    }
}
