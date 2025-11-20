using System.Diagnostics;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Constants;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.JobWorkflowService.Services;

/// <summary>
/// Background service that watches for trucks becoming available and assigns them to pending orders
/// </summary>
public class TruckAvailabilityHandler : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TruckAvailabilityHandler> _logger;
    private readonly ActivitySource _activitySource;

    public TruckAvailabilityHandler(
        IMessageConsumer messageConsumer,
        IMessagePublisher messagePublisher,
        IServiceProvider serviceProvider,
        ILogger<TruckAvailabilityHandler> logger)
    {
        _messageConsumer = messageConsumer;
        _messagePublisher = messagePublisher;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.JobWorkflowService");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Truck Availability Handler starting up");

        try
        {
            // Subscribe to truck idle events
            await _messageConsumer.StartConsumingAsync<TruckIdleEvent>(
                queueName: "job-workflow-service-truck-idle",
                handler: HandleTruckIdleAsync,
                exchangeName: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.Idle,
                cancellationToken: stoppingToken);

            _logger.LogInformation("Successfully subscribed to truck idle events");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Truck Availability Handler is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Truck Availability Handler");
            throw;
        }
    }

    private async Task HandleTruckIdleAsync(TruckIdleEvent truckEvent)
    {
        using var activity = _activitySource.StartActivity("HandleTruckIdle");
        activity?.SetTag("truck.id", truckEvent.TruckId);

        try
        {
            if (!int.TryParse(truckEvent.TruckId, out var truckId))
            {
                _logger.LogWarning("Invalid truck ID format: {TruckId}", truckEvent.TruckId);
                return;
            }

            _logger.LogInformation(
                "Truck {TruckId} became available - checking for pending orders",
                truckId);

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<JobWorkflowRepository>();

            // Get the next pending order
            var pendingOrders = await repository.GetPendingOrdersAsync(CancellationToken.None);

            if (pendingOrders.Count == 0)
            {
                _logger.LogInformation(
                    "No pending orders to assign to truck {TruckId}",
                    truckId);
                return;
            }

            var order = pendingOrders.First();

            // Get truck details
            var truck = await repository.GetTruckDetailsAsync(truckId, CancellationToken.None);
            if (truck == null)
            {
                _logger.LogWarning("Truck {TruckId} not found in database", truckId);
                return;
            }

            _logger.LogInformation(
                "Assigning truck {TruckId} (driver: {DriverName}) to pending order {OrderId}",
                truckId, truck.DriverName, order.Id);

            // Assign truck to order in database
            await repository.AssignTruckToOrderAsync(order.Id, truckId, CancellationToken.None);

            // Publish truck assigned event
            await _messagePublisher.PublishAsync(
                new TruckAssignedToOrderEvent
                {
                    OrderId = order.Id,
                    TruckId = truckId,
                    TruckDriverName = truck.DriverName
                },
                exchange: ExchangeNames.OrderEvents,
                routingKey: RoutingKeys.Order.TruckAssigned);

            _logger.LogInformation(
                "Successfully assigned newly available truck {TruckId} to order {OrderId}",
                truckId, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling truck idle event for truck {TruckId}",
                truckEvent.TruckId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck Availability Handler stopping");
        return base.StopAsync(cancellationToken);
    }
}
