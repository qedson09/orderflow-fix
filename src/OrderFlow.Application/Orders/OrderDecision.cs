using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public sealed record OrderDecision(Guid Id, string ClOrdId, string ExecId, string Symbol, OrderSide Side,
    decimal Quantity, decimal Price, OrderStatus Status, string? Reason, string AccountId = "CLIENTE-001")
{
    public static OrderDecision From(Order order) => new(order.Id, order.ClOrdId, order.ExecId,
        order.Symbol, order.Side, order.Quantity, order.Price, order.Status, order.Reason, order.AccountId);
}
