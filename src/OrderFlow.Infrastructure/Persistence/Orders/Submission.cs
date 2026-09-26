using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence.Orders;

public sealed class Submission
{
    public string AccountId { get; set; } = "CLIENTE-001";

    public string ClOrdId { get; set; } = "";

    public string Symbol { get; set; } = "";

    public OrderSide Side { get; set; }

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public string Status { get; set; } = "Pending";

    public string? Reason { get; set; }

    public string? OrderId { get; set; }

    public string? ExecId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public SubmissionView View() => new(ClOrdId, Symbol, Side, Quantity, Price, Status, Reason, OrderId, ExecId, CreatedAt, UpdatedAt, AccountId);

    public bool Matches(OrderRequest r) => new OrderRequest(ClOrdId, Symbol, Side, Quantity, Price, AccountId) == r;
}
