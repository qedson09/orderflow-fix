using OrderFlow.Domain.Exposures;
using System.Globalization;

namespace OrderFlow.Application.Exposures;

public sealed record ExposureSummary(string AccountId, string Symbol, string CurrentExposure,
    string AbsoluteLimit, string AvailableForBuy, string AvailableForSell, decimal UtilizationPercentage, long Version)
{
    public static ExposureSummary From(SymbolExposure value, decimal limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "O limite deve ser positivo.");
        static string Money(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);
        return new(value.AccountId, value.Symbol, Money(value.Amount), Money(limit),
            Money(limit - value.Amount), Money(limit + value.Amount),
            Math.Abs(value.Amount) / limit * 100m, value.Version);
    }
}
