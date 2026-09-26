namespace OrderFlow.Application.Orders;

public sealed record OrderPage(IReadOnlyList<ApiOrder> Items, int Page, int PageSize, int TotalCount, int TotalPages);
