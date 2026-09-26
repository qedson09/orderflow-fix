namespace OrderFlow.Application.Orders;

public sealed class IdempotencyConflictException() : Exception("ClOrdID já usado com outros dados.");
