using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public interface ISubmissionStore
{
    Task<SubmissionView> CreateAsync(OrderRequest request, CancellationToken cancellationToken);
    Task<SubmissionView?> FindAsync(string clOrdId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SubmissionView>> RecentAsync(CancellationToken cancellationToken);
    Task RecordAsync(OrderDecision decision, CancellationToken cancellationToken);
    Task<IReadOnlyList<SubmissionView>> PendingAsync(CancellationToken cancellationToken);
    Task MarkAttemptAsync(string clOrdId, CancellationToken cancellationToken);
}
