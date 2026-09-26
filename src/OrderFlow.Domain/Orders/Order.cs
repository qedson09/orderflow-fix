namespace OrderFlow.Domain.Orders;

public sealed class Order
{
    public string AccountId { get; private set; } = "CLIENTE-001";
    public Guid Id { get; private set; }
    public string SessionKey { get; private set; } = "";
    public string ClOrdId { get; private set; } = "";
    public string Symbol { get; private set; } = "";
    public OrderSide Side { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }
    public OrderStatus Status { get; private set; }
    public string ExecId { get; private set; } = "";
    public string? Reason { get; private set; }
    public string? InputError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    private Order() { }
    public static Order Decide(string session, OrderRequest request, string? rejection, string? inputError = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionKey = session,
        ClOrdId = request.ClOrdId,
        AccountId = request.AccountId,
        Symbol = request.Symbol,
        Side = request.Side,
        Quantity = request.Quantity,
        Price = request.Price,
        Status = rejection is null ? OrderStatus.New : OrderStatus.Rejected,
        Reason = rejection,
        InputError = inputError,
        ExecId = Guid.NewGuid().ToString("N"),
        CreatedAt = DateTime.UtcNow
    };
    public bool Matches(OrderRequest request) => request == new OrderRequest(ClOrdId, Symbol, Side, Quantity, Price, AccountId);
}
