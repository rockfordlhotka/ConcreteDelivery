using System.Diagnostics;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Constants;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.InventoryService.Services;

/// <summary>
/// Background service that listens for truck events and manages inventory
/// </summary>
public class TruckEventListener : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly InventoryManager _inventoryManager;
    private readonly ILogger<TruckEventListener> _logger;
    private readonly ActivitySource _activitySource;

    public TruckEventListener(
        IMessageConsumer messageConsumer,
        InventoryManager inventoryManager,
        ILogger<TruckEventListener> logger)
    {
        _messageConsumer = messageConsumer;
        _inventoryManager = inventoryManager;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.InventoryService");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inventory Service starting up - listening for truck events");

        try
        {
            // Subscribe to truck status change events
            await _messageConsumer.StartConsumingAsync<TruckStatusChangedEvent>(
                queueName: "inventory-service-truck-status",
                handler: HandleTruckStatusChangedAsync,
                exchangeName: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.StatusChanged,
                cancellationToken: stoppingToken);

            _logger.LogInformation("Successfully subscribed to truck status events");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Inventory Service is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Inventory Service");
            throw;
        }
    }

    private async Task HandleTruckStatusChangedAsync(TruckStatusChangedEvent truckEvent)
    {
        using var activity = _activitySource.StartActivity("HandleTruckStatusChanged");
        activity?.SetTag("truck.id", truckEvent.TruckId);
        activity?.SetTag("status.previous", truckEvent.PreviousStatus);
        activity?.SetTag("status.new", truckEvent.NewStatus);

        try
        {
            _logger.LogInformation(
                "Received truck status change: Truck {TruckId} changed from {PreviousStatus} to {NewStatus}",
                truckEvent.TruckId, truckEvent.PreviousStatus, truckEvent.NewStatus);

            // Check if truck is starting to load materials
            if (truckEvent.NewStatus.Equals(TruckStatus.Loading, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Truck {TruckId} is loading - deducting materials from inventory", 
                    truckEvent.TruckId);

                var success = await _inventoryManager.DeductMaterialsForTruckAsync(
                    truckEvent.TruckId, 
                    CancellationToken.None);

                if (success)
                {
                    _logger.LogInformation(
                        "Successfully processed material deduction for truck {TruckId}", 
                        truckEvent.TruckId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to deduct materials for truck {TruckId} - check logs for details", 
                        truckEvent.TruckId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error handling truck status change for truck {TruckId}", 
                truckEvent.TruckId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Inventory Service stopping");
        await base.StopAsync(cancellationToken);
    }
}
