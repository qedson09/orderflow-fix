namespace OrderFlow.Domain.Orders;

public sealed record OrderRequest(string ClOrdId, string Symbol, OrderSide Side, decimal Quantity, decimal Price, string AccountId = "CLIENTE-001");