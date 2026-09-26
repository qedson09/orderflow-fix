using Microsoft.AspNetCore.Mvc;

namespace OrderGenerator.Validation;

public static class ApiValidationResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var entry in context.ModelState.Where(x => x.Value?.Errors.Count > 0))
        {
            var field = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.Key.TrimStart('$', '.'));

            var message = field switch
            {
                "quantity" => "Informe uma quantidade inteira entre 1 e 99.999.",
                "price" => "Informe o preço como texto decimal, por exemplo 35.50.",
                "side" => "Selecione Compra (B) ou Venda (S).",
                "symbol" => "Selecione um ativo válido: PETR4, VALE3 ou VIIA4.",
                "accountId" => "Selecione um cliente válido.",
                "clOrdId" => "Informe um identificador de ordem válido.",
                "page" or "pageSize" => "Informe um número inteiro válido para a paginação.",
                _ => "Não foi possível ler os dados enviados. Confira o preenchimento e tente novamente."
            };

            errors[string.IsNullOrEmpty(field) || field == "input" ? "request" : field] = [message];
        }

        var problem = new ValidationProblemDetails(errors)
        {
            Status = 400,
            Title = "Dados da solicitação inválidos"
        };

        problem.Extensions["code"] = context.HttpContext.Request.Method == "POST" &&
            context.HttpContext.Request.Path == "/api/orders" ? "ORDER_VALIDATION" : "VALIDATION_ERROR";

        return new BadRequestObjectResult(problem);
    }
}
