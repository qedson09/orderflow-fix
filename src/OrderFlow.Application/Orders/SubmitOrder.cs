using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public sealed class SubmitOrder(ISubmissionStore store)
{
    public Task<SubmissionView> ExecuteAsync(OrderRequest request, CancellationToken cancellationToken)
    {
        if (OrderRules.Validate(request) is { } error) throw new ArgumentException(error);
        return store.CreateAsync(request, cancellationToken);
    }
}
