namespace OrderFlow.Domain.Orders;

public static class OrderSideCode
{
    public static string ToCode(this OrderSide side) => side switch
    {
        OrderSide.Buy => "B",
        OrderSide.Sell => "S",
        _ => throw new ArgumentException("Lado deve ser B ou S.", nameof(side))
    };

    public static OrderSide Parse(string code) => code switch
    {
        "B" => OrderSide.Buy,
        "S" => OrderSide.Sell,
        _ => throw new ArgumentException("Lado deve ser B ou S.", nameof(code))
    };
}
