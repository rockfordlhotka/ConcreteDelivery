using ConcreteDelivery.Data;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.TruckStatusService.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ConcreteDelivery.TruckStatusService"))
    .WithTracing(tracing => tracing
        .AddSource("ConcreteDelivery.TruckStatusService")
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("ConcreteDelivery.TruckStatusService")
        .AddConsoleExporter());

// Configure logging with OpenTelemetry
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddConsoleExporter();
});
builder.Logging.AddConsole();

// Add database context
builder.Services.AddDbContextFactory<ConcreteDeliveryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add RabbitMQ messaging
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// Add truck status repository
builder.Services.AddScoped<TruckStatusRepository>();

// Add background service
builder.Services.AddHostedService<TruckSimulationService>();

var host = builder.Build();
host.Run();
