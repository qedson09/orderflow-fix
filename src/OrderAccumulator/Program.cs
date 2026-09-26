using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Fix;
using OrderFlow.Application.Exposures;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Persistence.Configuration;
using OrderFlow.Infrastructure.Persistence.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders(); builder.Logging.AddJsonConsole();

builder.Services.AddProblemDetails();

builder.Services.AddControllers(options => options.Filters.Add<OrderAccumulator.Logging.ControllerLoggingFilter>());

builder.Services.AddAccumulatorDatabase(builder.Configuration.GetConnectionString("Trading")

    ?? throw new InvalidOperationException("Configure ConnectionStrings__Trading."));

builder.Services.AddScoped<IAccumulatorUnitOfWork, PostgresAccumulatorUnitOfWork>();

builder.Services.AddScoped<IExposureLimitProvider, OrderFlow.Infrastructure.Exposures.FixedExposureLimitProvider>();

builder.Services.AddScoped<IOrderProcessor, ProcessOrder>();

builder.Services.AddSingleton<AccumulatorFixApplication>();

builder.Services.AddSingleton<AcceptorService>();

builder.Services.AddHostedService(s => s.GetRequiredService<AcceptorService>());

builder.Services.AddKafkaCommon(builder.Configuration);

builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

builder.Services.AddScoped<OutboxPump>();

builder.Services.AddHostedService<OutboxPublisher>();

var app = builder.Build();

app.UseExceptionHandler();

// Read-only internal HTTP channel. Orders and decisions continue exclusively over FIX.
app.MapControllers();

await DatabaseSetup.MigrateAsync<AccumulatorDb>(app.Services);

await app.RunAsync();
