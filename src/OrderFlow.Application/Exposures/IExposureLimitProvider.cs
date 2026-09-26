namespace OrderFlow.Application.Exposures;

public interface IExposureLimitProvider
{
    Task<decimal> GetLimitAsync(string accountId, string symbol, CancellationToken cancellationToken = default);
}
