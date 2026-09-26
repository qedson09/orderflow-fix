using OrderFlow.Domain.Exposures;
using OrderFlow.Domain.Orders;
using System.Globalization;
using System.Text.Json;

namespace OrderFlow.Application.Orders;

public sealed record OrderDecisionEvent(int SchemaVersion, Guid EventId, string EventType, string SessionKey,
    string ClOrdId, Guid OrderId, string ExecId, string Symbol, string Side, decimal Quantity, string Price,
    string Decision, string? Reason, string? ExposureAfter, long? SymbolSequence, DateTime OccurredAtUtc, string AccountId = "CLIENTE-001")
{
    public static OrderDecisionEvent Create(Order order, SymbolExposure? exposure) => new(1, Guid.NewGuid(),
        "OrderDecisionRecorded", order.SessionKey, order.ClOrdId, order.Id, order.ExecId, order.Symbol,
        order.Side.ToCode(), order.Quantity, order.Price.ToString(CultureInfo.InvariantCulture),
        order.Status == OrderStatus.New ? "Accepted" : "Rejected", order.Reason,
        exposure?.Amount.ToString("F2", CultureInfo.InvariantCulture), exposure?.DecisionSequence, order.CreatedAt, order.AccountId);

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
}
