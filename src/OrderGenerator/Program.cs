using OrderGenerator.Validation;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Fix;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Persistence.Configuration;
using OrderFlow.Infrastructure.Persistence.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders(); builder.Logging.AddJsonConsole();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 16 * 1024);

builder.Services.AddProblemDetails();

builder.Services.AddControllers(options => options.Filters.Add<OrderGenerator.Logging.ControllerLoggingFilter>()).ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ApiValidationResponse.Create);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("exposures", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Accumulator:HttpUrl"] ?? "http://localhost:8081");

    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:3000")
    .WithMethods("GET", "POST").WithHeaders("Content-Type")));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OrderGenerator.RateLimit")
            .LogWarning("Limite de requisições excedido {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);

        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? (int)Math.Ceiling(retry.TotalSeconds) : 60;

        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        await Results.Problem(statusCode: 429, title: "Limite de requisições", extensions: new Dictionary<string, object?> { ["code"] = "RATE_LIMIT" })
            .ExecuteAsync(context.HttpContext);
    };

    o.AddFixedWindowLimiter("write", l => { l.PermitLimit = 60; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });

    o.AddFixedWindowLimiter("read", l => { l.PermitLimit = 600; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });
});

builder.Services.AddGeneratorDatabase(builder.Configuration.GetConnectionString("Trading")
    ?? throw new InvalidOperationException("Configure ConnectionStrings__Trading."));

builder.Services.AddScoped<ISubmissionStore, PostgresSubmissionStore>();

builder.Services.AddScoped<SubmitOrder>();

builder.Services.AddSingleton<GeneratorFixApplication>();

builder.Services.AddSingleton<IOrderTransport>(s => s.GetRequiredService<GeneratorFixApplication>());

builder.Services.AddHostedService<InitiatorService>();

builder.Services.AddHostedService<SubmissionDispatcher>();

builder.Services.AddKafkaCommon(builder.Configuration);

builder.Services.AddScoped<AuditInbox>(); builder.Services.AddSingleton<AuditHealth>();

builder.Services.AddHostedService<AuditConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.UseExceptionHandler(); app.UseStatusCodePages();

app.Use(async (ctx, next) => { ctx.Response.Headers["X-Content-Type-Options"] = "nosniff"; ctx.Response.Headers.CacheControl = "no-store"; await next(ctx); });

app.UseRouting();

app.UseCors(); app.UseRateLimiter();

app.MapControllers();

await DatabaseSetup.MigrateAsync<GeneratorDb>(app.Services);

await app.RunAsync();

public partial class Program { }
