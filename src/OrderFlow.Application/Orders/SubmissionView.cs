using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public sealed record SubmissionView(string ClOrdId, string Symbol, OrderSide Side, decimal Quantity,
    decimal Price, string Status, string? Reason, string? OrderId, string? ExecId, DateTime CreatedAt, DateTime? UpdatedAt, string AccountId = "CLIENTE-001");
