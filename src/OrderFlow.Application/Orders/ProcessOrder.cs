using OrderFlow.Application.Exposures;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Customers;
using OrderFlow.Domain.Exposures;

namespace OrderFlow.Application.Orders;

public sealed class ProcessOrder(IAccumulatorUnitOfWork store, IExposureLimitProvider limits) : IOrderProcessor
{
    public async Task<OrderDecision> ProcessAsync(string session, OrderRequest request,
        string? protocolBusinessError = null, CancellationToken cancellationToken = default)
    {
        await using var tx = await store.BeginAsync(session, request.ClOrdId, cancellationToken);

        var previous = await store.FindOrderAsync(session, request.ClOrdId, cancellationToken);

        if (previous is not null)
        {
            await tx.CommitAsync(cancellationToken);

            if (!previous.Matches(request) || previous.InputError != protocolBusinessError)
                throw new IdempotencyConflictException();

            return OrderDecision.From(previous);
        }

        var error = protocolBusinessError ?? OrderRules.Validate(request);

        SymbolExposure? exposure = null;

        if (OrderRules.Symbols.Contains(request.Symbol) && CustomerCatalog.Contains(request.AccountId))
        {
            exposure = await store.LockExposureAsync(request.Symbol, cancellationToken, request.AccountId);
            if (error is null)
            {
                var limit = await limits.GetLimitAsync(request.AccountId, request.Symbol, cancellationToken);
                if (!exposure.TryApply(request, limit))
                    error = $"Limite de exposição absoluta de {limit.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} excedido.";
            }
            exposure.RegisterDecision();
        }

        var order = Order.Decide(session, request, error, protocolBusinessError);

        store.Add(order);
        store.AddEvent(OrderDecisionEvent.Create(order, exposure));

        await store.SaveAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return OrderDecision.From(order);
    }
}
