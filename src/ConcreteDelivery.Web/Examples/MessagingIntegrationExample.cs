using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Constants;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.Web.Examples;

/// <summary>
/// Example showing how to integrate RabbitMQ messaging in the Web application
/// </summary>
public static class MessagingIntegrationExample
{
    /// <summary>
    /// Add this to Program.cs to register messaging services
    /// </summary>
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Add RabbitMQ messaging (configuration comes from user secrets)
        builder.Services.AddRabbitMqMessaging(builder.Configuration);
    }

    /// <summary>
    /// Example: Publishing a command when dispatcher sends a truck to job site
    /// </summary>
    public class TruckDispatchService
    {
        private readonly IMessagePublisher _publisher;
        private readonly ILogger<TruckDispatchService> _logger;

        public TruckDispatchService(
            IMessagePublisher publisher,
            ILogger<TruckDispatchService> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task DispatchTruckToJobSite(string truckId, string jobSiteId, string address)
        {
            // Send command to truck
            var command = new EnrouteToJobSiteCommand
            {
                TruckId = truckId,
                JobSiteId = jobSiteId,
                Address = address
            };

            var routingKey = $"truck.{command.TruckId}.enroute";
            await _publisher.PublishAsync(command, ExchangeNames.TruckCommands, routingKey);

            _logger.LogInformation(
                "Dispatched truck {TruckId} to job site {JobSiteId}",
                truckId, jobSiteId);
        }

        public async Task StartPouringAtJobSite(string truckId, string jobSiteId)
        {
            var command = new StartPouringCommand
            {
                TruckId = truckId,
                JobSiteId = jobSiteId
            };

            var routingKey = $"truck.{command.TruckId}.startpouring";
            await _publisher.PublishAsync(command, ExchangeNames.TruckCommands, routingKey);

            _logger.LogInformation(
                "Sent start pouring command for truck {TruckId} at job site {JobSiteId}",
                truckId, jobSiteId);
        }
    }

    /// <summary>
    /// Example: Background service that consumes truck events
    /// </summary>
    public class TruckEventMonitor : BackgroundService
    {
        private readonly IMessageConsumer _consumer;
        private readonly ILogger<TruckEventMonitor> _logger;

        public TruckEventMonitor(
            IMessageConsumer consumer,
            ILogger<TruckEventMonitor> logger)
        {
            _consumer = consumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Subscribe to all truck status changes
            await _consumer.StartConsumingAsync<TruckStatusChangedEvent>(
                queueName: "web.truck.statuschanged",
                handler: HandleStatusChanged,
                exchangeName: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.StatusChanged,
                cancellationToken: stoppingToken);
        }

        private async Task HandleStatusChanged(TruckStatusChangedEvent evt)
        {
            _logger.LogInformation(
                "Truck {TruckId} status changed from {OldStatus} to {NewStatus}",
                evt.TruckId, evt.PreviousStatus, evt.NewStatus);

            // Update dashboard, notify dispatcher, etc.
            await Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _consumer.StopConsumingAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}
