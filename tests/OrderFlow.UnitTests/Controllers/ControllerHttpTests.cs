using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Orders;
using OrderGenerator.Controllers;
using OrderGenerator.Logging;
using OrderGenerator.Validation;

namespace OrderFlow.UnitTests.Controllers;

public sealed class ControllerHttpTests : IAsyncLifetime
{
    private WebApplication app = null!;

    private HttpClient client = null!;

    private readonly CapturingLoggerProvider logs = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.AddProvider(logs);

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddProblemDetails();

        builder.Services.AddControllers(o => o.Filters.Add<ControllerLoggingFilter>())
            .AddApplicationPart(typeof(OrdersController).Assembly)
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ApiValidationResponse.Create);

        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<ISubmissionStore, ControllerSubmissionStore>();

        builder.Services.AddScoped<SubmitOrder>();

        builder.Services.AddRateLimiter(o =>
        {
            o.AddFixedWindowLimiter("read", l => { l.PermitLimit = 100; l.Window = TimeSpan.FromMinutes(1); });
            o.AddFixedWindowLimiter("write", l => { l.PermitLimit = 100; l.Window = TimeSpan.FromMinutes(1); });
        });

        app = builder.Build();

        app.UseExceptionHandler();

        app.UseSwagger();

        app.UseRouting();

        app.UseRateLimiter();

        app.MapControllers();

        await app.StartAsync();

        client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    }

    [Theory]
    [InlineData("\"C\"", "10", "\"35.50\"", "side")]
    [InlineData("\"B\"", "1.5", "\"35.50\"", "quantity")]
    [InlineData("\"B\"", "10", "35.50", "price")]
    [InlineData("null", "10", "\"35.50\"", "side")]
    public async Task Invalid_fields_return_friendly_problem(string side, string quantity, string price, string field)
    {
        var json = $$"""{"clOrdId":"{{Guid.NewGuid():D}}","symbol":"PETR4","side":{{side}},"quantity":{{quantity}},"price":{{price}},"accountId":"CLIENTE-001"}""";

        var response = await client.PostAsync("/api/orders", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("ORDER_VALIDATION", body.GetProperty("code").GetString());

        Assert.True(body.GetProperty("errors").TryGetProperty(field, out var messages));

        Assert.False(string.IsNullOrWhiteSpace(messages[0].GetString()));

        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Create_query_and_conflict_preserve_contract()
    {
        var input = new OrderInput(Guid.NewGuid().ToString("D"), "PETR4", "B", 10, "35.50");

        var response = await client.PostAsJsonAsync("/api/orders", input);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        Assert.Equal($"/api/orders/{input.ClOrdId}", response.Headers.Location?.ToString());

        var order = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{input.ClOrdId}");

        Assert.Equal("Pending", order.GetProperty("status").GetString());

        Assert.Equal("B", order.GetProperty("side").GetString());

        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("persistida"));

        var conflict = await client.PostAsJsonAsync("/api/orders", input with { Quantity = 11 });

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Swagger_and_liveness_remain_available()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);

        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");

        Assert.True(swagger.GetProperty("paths").GetProperty("/api/orders").TryGetProperty("post", out _));

        Assert.True(swagger.GetProperty("paths").TryGetProperty("/api/accounts/{accountId}/exposures", out _));
    }

    [Fact]
    public async Task Unexpected_failure_is_logged_and_returns_problem_without_details()
    {
        ((ControllerSubmissionStore)app.Services.GetRequiredService<ISubmissionStore>()).FailReads = true;

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        Assert.DoesNotContain("Falha simulada", await response.Content.ReadAsStringAsync());

        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }
}
