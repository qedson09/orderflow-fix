using OrderFlow.Application.Exposures;

namespace OrderFlow.UnitTests.Exposures;

internal sealed class TestLimitProvider(decimal limit) : IExposureLimitProvider
{
    public int Calls { get; private set; }

    public decimal Limit { get; set; } = limit;

    public string? AccountId { get; private set; }

    public string? Symbol { get; private set; }

    public bool Fail { get; set; }

    public Task<decimal> GetLimitAsync(string accountId, string symbol, CancellationToken cancellationToken = default)
    {
        Calls++;

        AccountId = accountId;

        Symbol = symbol;

        if (Fail) throw new IOException("Provider indisponível");

        return Task.FromResult(Limit);
    }
}
