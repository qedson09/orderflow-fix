namespace OrderFlow.Application.Exposures;

public sealed record ExposureResponse(string AccountId, DateTime CheckedAt, IReadOnlyList<ExposureSummary> Items);
