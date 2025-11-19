using ConcreteDelivery.Data;
using ConcreteDelivery.JobWorkflowService.Services;
using ConcreteDelivery.Messaging;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ConcreteDelivery.JobWorkflowService"))
    .WithTracing(tracing => tracing
        .AddSource("ConcreteDelivery.JobWorkflowService")
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("ConcreteDelivery.JobWorkflowService")
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

// Add repository
builder.Services.AddScoped<JobWorkflowRepository>();

// Add background services
builder.Services.AddHostedService<JobWorkflowOrchestrator>();
builder.Services.AddHostedService<TruckAvailabilityHandler>();
builder.Services.AddHostedService<OrderCancellationHandler>();
builder.Services.AddHostedService<OrderCompletionHandler>();

var host = builder.Build();
host.Run();
