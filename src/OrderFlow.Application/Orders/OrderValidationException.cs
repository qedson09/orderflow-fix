namespace OrderFlow.Application.Orders;

public sealed class OrderValidationException(Dictionary<string, string[]> errors)
    : ArgumentException("Confira os campos informados e tente novamente.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
