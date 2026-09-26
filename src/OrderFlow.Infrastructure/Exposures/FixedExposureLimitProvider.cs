using OrderFlow.Application.Exposures;

namespace OrderFlow.Infrastructure.Exposures;

public sealed class FixedExposureLimitProvider : IExposureLimitProvider
{
    private const decimal Limit = 100_000_000m;

    public Task<decimal> GetLimitAsync(string accountId, string symbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Limit);
    }
}
