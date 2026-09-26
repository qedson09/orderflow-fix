using OrderFlow.Application.Orders;

namespace OrderFlow.UnitTests;

public sealed class OrderInputValidationTests
{
    private static OrderInput Valid() => new(Guid.NewGuid().ToString("D"), "PETR4", "B", 10, "35.50");

    [Fact]
    public void InvalidSideReturnsFriendlyFieldError()
    {
        var error = Assert.Throws<OrderValidationException>(() => (Valid() with { Side = "C" }).ToDomain());

        Assert.Equal("Selecione Compra (B) ou Venda (S). O lado informado é inválido.", error.Errors["side"][0]);

        Assert.Single(error.Errors);
    }

    [Fact]
    public void AllInvalidFieldsAreReportedTogether()
    {
        var input = new OrderInput("invalid", "XYZ", "C", 0, "0.00", "missing");

        var error = Assert.Throws<OrderValidationException>(() => input.ToDomain());

        Assert.Equal(6, error.Errors.Count);

        foreach (var key in new[] { "clOrdId", "symbol", "side", "quantity", "price", "accountId" })
            Assert.NotEmpty(error.Errors[key]);
    }

    [Theory]
    [InlineData(null)][InlineData("")][InlineData("35,50")][InlineData("abc")][InlineData("1.001")]
    public void MissingOrInvalidPriceIsReportedAsFieldError(string? value)
    {
        var error = Assert.Throws<OrderValidationException>(() => (Valid() with { Price = value! }).ToDomain());

        Assert.Single(error.Errors); Assert.Contains("R$ 0,01", error.Errors["price"][0]);
    }
}
