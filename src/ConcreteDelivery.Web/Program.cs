using ConcreteDelivery.Data;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Web.Components;
using ConcreteDelivery.Web.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ConcreteDelivery.Web"))
    .WithTracing(tracing => tracing
        .AddSource("ConcreteDelivery.Web")
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("ConcreteDelivery.Web")
        .AddAspNetCoreInstrumentation()
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

// Add database context factory for concurrent access
builder.Services.AddDbContextFactory<ConcreteDeliveryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add RabbitMQ messaging
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// Add dashboard service as singleton for real-time updates
builder.Services.AddSingleton<TruckDashboardService>();

// Add order service as scoped
builder.Services.AddScoped<OrderService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Only redirect to HTTPS in development (Tailscale handles SSL in production)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
