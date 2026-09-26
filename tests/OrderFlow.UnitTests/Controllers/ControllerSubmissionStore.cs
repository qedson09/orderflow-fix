using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.UnitTests.Controllers;

internal sealed class ControllerSubmissionStore : ISubmissionStore
{
    private SubmissionView? submission;

    public bool FailReads { get; set; }

    public Task<SubmissionView> CreateAsync(OrderRequest request, CancellationToken cancellationToken)
    {
        if (submission is not null && submission.Quantity != request.Quantity) throw new IdempotencyConflictException();

        submission ??= new SubmissionView(request.ClOrdId, request.Symbol, request.Side, request.Quantity,
            request.Price, "Pending", null, null, null, DateTime.UtcNow, null, request.AccountId);

        return Task.FromResult(submission);
    }

    public Task<SubmissionView?> FindAsync(string clOrdId, CancellationToken cancellationToken) => FailReads ? throw new InvalidOperationException("Falha simulada no armazenamento") : Task.FromResult(submission?.ClOrdId == clOrdId ? submission : null);

    public Task<IReadOnlyList<SubmissionView>> RecentAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SubmissionView>>([]);

    public Task<IReadOnlyList<SubmissionView>> PendingAsync(CancellationToken cancellationToken) => RecentAsync(cancellationToken);

    public Task RecordAsync(OrderDecision decision, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MarkAttemptAsync(string clOrdId, CancellationToken cancellationToken) => Task.CompletedTask;
}
