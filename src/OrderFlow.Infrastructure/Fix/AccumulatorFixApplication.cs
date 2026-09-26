using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Orders;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace OrderFlow.Infrastructure.Fix;

public sealed class AccumulatorFixApplication(IServiceScopeFactory scopes, ILogger<AccumulatorFixApplication> logger)
    : FixApplicationBase(logger)
{
    public void OnMessage(NewOrderSingle message, SessionID sessionID)
    {
        // Required FIX fields are read outside the catch: engine must issue protocol Rejects when missing.
        var request = FixMapper.FromMessage(message);

        var typeError = message.OrdType.Value != OrdType.LIMIT ||
            (message.IsSetField(Tags.TimeInForce) && message.TimeInForce.Value != TimeInForce.DAY)
            ? "Somente ordens Limit com validade Day são suportadas." : null;

        try
        {
            using var scope = scopes.CreateScope();

            var processor = scope.ServiceProvider.GetRequiredService<IOrderProcessor>();

            var decision = processor.ProcessAsync(sessionID.ToString(), request, typeError).GetAwaiter().GetResult();

            var sent = Session.SendToTarget(FixMapper.ToMessage(decision), sessionID);

            logger.LogInformation("Ordem {ClOrdId}: {Status}; resposta enviada: {Sent}", request.ClOrdId, decision.Status, sent);
        }
        catch (IdempotencyConflictException ex)
        {
            var reject = new BusinessMessageReject();

            reject.SetField(new RefMsgType("D"));

            reject.SetField(new BusinessRejectReason(BusinessRejectReason.OTHER));

            reject.SetField(new BusinessRejectRefID(request.ClOrdId));

            reject.SetField(new Text("ClOrdID reused with different payload; original decision unchanged."));

            Session.SendToTarget(reject, sessionID);

            logger.LogWarning(ex, "FIX conflito de identidade {ClOrdId}", request.ClOrdId);
        }
        catch (Exception ex)
        {
            // Unknown technical outcome must never become a business rejection.
            // The generator retries the same persisted identity until it gets a durable response.
            logger.LogError(ex, "Falha ao processar/responder {ClOrdId}; reenvio idempotente poderá recuperar", request.ClOrdId);
        }
    }
}
