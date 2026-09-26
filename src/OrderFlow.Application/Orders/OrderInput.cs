using System.Globalization;
using System.Text.RegularExpressions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Customers;

namespace OrderFlow.Application.Orders;

public sealed record OrderInput(string ClOrdId, string Symbol, string Side, int Quantity, string Price, string AccountId = "CLIENTE-001")
{
    public OrderRequest ToDomain()
    {
        var errors = new Dictionary<string, string[]>();
        if (ClOrdId is null || !Regex.IsMatch(ClOrdId, "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"))
            errors["clOrdId"] = ["O identificador da ordem é inválido. Atualize a página antes de criar uma nova solicitação."];
        if (!CustomerCatalog.Contains(AccountId))
            errors["accountId"] = ["Selecione um cliente válido."];
        if (!OrderRules.Symbols.Contains(Symbol))
            errors["symbol"] = ["Selecione um ativo válido: PETR4, VALE3 ou VIIA4."];
        if (Side is not ("B" or "S"))
            errors["side"] = ["Selecione Compra (B) ou Venda (S). O lado informado é inválido."];
        if (Quantity is < 1 or > 99999)
            errors["quantity"] = ["Informe uma quantidade inteira entre 1 e 99.999."];
        var validPrice = Price is not null && Regex.IsMatch(Price, @"^(0|[1-9][0-9]{0,2})\.[0-9]{2}$") &&
            decimal.TryParse(Price, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) && parsed > 0;
        if (!validPrice)
            errors["price"] = ["Informe um preço entre R$ 0,01 e R$ 999,99. No envio, use duas casas decimais e ponto, por exemplo 35.50."];
        if (errors.Count > 0) throw new OrderValidationException(errors);
        return new(ClOrdId!, Symbol, OrderSideCode.Parse(Side), Quantity,
            decimal.Parse(Price!, CultureInfo.InvariantCulture), AccountId);
    }
}
