using OrderFlow.Domain.Exposures;

namespace OrderFlow.Infrastructure.Persistence.Configuration;

public static class ExposureQueries
{
    // Version increases only when an accepted order changes exposure, never for rejection.
    // Do not filter Amount != 0: an asset remains traded after offsetting buy/sell orders.
    public static IQueryable<SymbolExposure> TradedBy(this IQueryable<SymbolExposure> exposures, string accountId) =>
        exposures.Where(x => x.AccountId == accountId && x.Version > 0).OrderBy(x => x.Symbol);
}
