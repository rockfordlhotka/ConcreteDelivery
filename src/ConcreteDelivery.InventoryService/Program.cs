using ConcreteDelivery.Data;
using ConcreteDelivery.InventoryService;
using ConcreteDelivery.InventoryService.Services;
using ConcreteDelivery.Messaging;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ConcreteDelivery.InventoryService"))
    .WithTracing(tracing => tracing
        .AddSource("ConcreteDelivery.InventoryService")
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("ConcreteDelivery.InventoryService")
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

// Add inventory management service
builder.Services.AddSingleton<InventoryManager>();

// Add background service
builder.Services.AddHostedService<TruckEventListener>();

var host = builder.Build();
host.Run();
