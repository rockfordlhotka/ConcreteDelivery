using System.Collections.Concurrent;
using System.Diagnostics;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.TruckStatusService.Services;

/// <summary>
/// Background service that simulates truck operations with compressed time for demo purposes
/// </summary>
/// <remarks>
/// Simulation timings (total ~80 seconds for average delivery):
/// - Loading: 15 seconds
/// - Travel to job site: ~2 seconds per mile (varies by distance)
/// - Delivery/Pouring: 15 seconds
/// - Travel back to yard: ~2 seconds per mile
/// - Washing: 10 seconds
/// </remarks>
public class TruckSimulationService : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TruckSimulationService> _logger;
    private readonly ActivitySource _activitySource;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeTrucks;

    // Simulation timing constants (in seconds)
    private const int LoadingTimeSeconds = 15;
    private const int DeliveryTimeSeconds = 15;
    private const int WashingTimeSeconds = 10;
    private const int SecondsPerMile = 2;

    public TruckSimulationService(
        IMessageConsumer messageConsumer,
        IMessagePublisher messagePublisher,
        IServiceProvider serviceProvider,
        ILogger<TruckSimulationService> logger)
    {
        _messageConsumer = messageConsumer;
        _messagePublisher = messagePublisher;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.TruckStatusService");
        _activeTrucks = new ConcurrentDictionary<int, CancellationTokenSource>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Truck Status Service starting up - listening for truck assignments");

        try
        {
            // Subscribe to truck assignment events
            await _messageConsumer.StartConsumingAsync<TruckAssignedToOrderEvent>(
                queueName: "truck-status-service-assignments",
                handler: HandleTruckAssignmentAsync,
                exchangeName: "order-events",
                routingKey: "order.truck.assigned",
                cancellationToken: stoppingToken);

            _logger.LogInformation("Successfully subscribed to truck assignment events");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Truck Status Service is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Truck Status Service");
            throw;
        }
    }

    private async Task HandleTruckAssignmentAsync(TruckAssignedToOrderEvent assignmentEvent)
    {
        using var activity = _activitySource.StartActivity("HandleTruckAssignment");
        activity?.SetTag("truck.id", assignmentEvent.TruckId);
        activity?.SetTag("order.id", assignmentEvent.OrderId);

        try
        {
            _logger.LogInformation(
                "Truck {TruckId} ({DriverName}) assigned to order {OrderId} - starting simulation",
                assignmentEvent.TruckId, assignmentEvent.TruckDriverName, assignmentEvent.OrderId);

            // Create a cancellation token for this specific truck simulation
            var truckCts = new CancellationTokenSource();
            _activeTrucks[assignmentEvent.TruckId] = truckCts;

            // Start the simulation in a background task
            _ = Task.Run(async () =>
            {
                try
                {
                    await SimulateTruckWorkflowAsync(
                        assignmentEvent.TruckId,
                        assignmentEvent.OrderId,
                        truckCts.Token);
                }
                finally
                {
                    _activeTrucks.TryRemove(assignmentEvent.TruckId, out _);
                    truckCts.Dispose();
                }
            }, truckCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling truck assignment for truck {TruckId} and order {OrderId}",
                assignmentEvent.TruckId, assignmentEvent.OrderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    private async Task SimulateTruckWorkflowAsync(
        int truckId,
        int orderId,
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("SimulateTruckWorkflow");
        activity?.SetTag("truck.id", truckId);
        activity?.SetTag("order.id", orderId);

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TruckStatusRepository>();

        try
        {
            // Get order details to determine travel time
            var order = await repository.GetOrderForTruckAsync(truckId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("No order found for truck {TruckId}", truckId);
                return;
            }

            var travelTimeToSite = order.DistanceMiles * SecondsPerMile;
            var travelTimeToYard = order.DistanceMiles * SecondsPerMile;
            var totalTime = LoadingTimeSeconds + travelTimeToSite + DeliveryTimeSeconds + 
                           travelTimeToYard + WashingTimeSeconds;

            _logger.LogInformation(
                "Starting workflow for truck {TruckId} - Total estimated time: {TotalTime}s " +
                "(Loading: {Loading}s, Travel to site: {TravelTo}s, Delivery: {Delivery}s, " +
                "Travel to yard: {TravelBack}s, Wash: {Wash}s)",
                truckId, totalTime, LoadingTimeSeconds, travelTimeToSite, DeliveryTimeSeconds,
                travelTimeToYard, WashingTimeSeconds);

            // Phase 1: Loading
            await SimulateLoadingAsync(truckId, orderId, repository, cancellationToken);

            // Phase 2: Travel to job site
            await SimulateTravelToSiteAsync(truckId, orderId, travelTimeToSite, repository, cancellationToken);

            // Phase 3: Delivery/Pouring
            await SimulateDeliveryAsync(truckId, orderId, repository, cancellationToken);

            // Phase 4: Travel back to yard
            await SimulateTravelToYardAsync(truckId, orderId, travelTimeToYard, repository, cancellationToken);

            // Phase 5: Washing
            await SimulateWashingAsync(truckId, orderId, repository, cancellationToken);

            // Phase 6: Return to available status
            await CompleteWorkflowAsync(truckId, orderId, repository, cancellationToken);

            _logger.LogInformation(
                "Completed workflow for truck {TruckId} and order {OrderId}",
                truckId, orderId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Workflow cancelled for truck {TruckId}",
                truckId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in workflow for truck {TruckId}",
                truckId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    private async Task SimulateLoadingAsync(
        int truckId,
        int orderId,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Starting loading phase ({Time}s)", 
            truckId, LoadingTimeSeconds);

        // Update status to Loading
        await repository.UpdateTruckStatusAsync(truckId, "Loading", orderId, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, "Loading", cancellationToken);

        // Publish truck status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = "Assigned",
                NewStatus = "Loading"
            },
            exchange: "truck-events",
            routingKey: "truck.status.changed");

        // Simulate loading time
        await Task.Delay(TimeSpan.FromSeconds(LoadingTimeSeconds), cancellationToken);

        // Publish materials loaded event
        await _messagePublisher.PublishAsync(
            new MaterialsLoadedEvent
            {
                TruckId = truckId.ToString(),
                Materials = new Dictionary<string, decimal>
                {
                    { "Concrete", 10 }, // 10 cubic yards
                    { "Sand", 5 },
                    { "Gravel", 5 }
                }
            },
            exchange: "truck-events",
            routingKey: "truck.materials.loaded");

        _logger.LogInformation("Truck {TruckId}: Loading complete", truckId);
    }

    private async Task SimulateTravelToSiteAsync(
        int truckId,
        int orderId,
        int travelTimeSeconds,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Traveling to job site ({Time}s)", 
            truckId, travelTimeSeconds);

        // Update status to EnRoute
        await repository.UpdateTruckStatusAsync(truckId, "EnRoute", orderId, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, "InTransit", cancellationToken);

        // Publish status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = "Loading",
                NewStatus = "EnRoute"
            },
            exchange: "truck-events",
            routingKey: "truck.status.changed");

        // Publish order in transit event
        await _messagePublisher.PublishAsync(
            new OrderInTransitEvent
            {
                OrderId = orderId,
                TruckId = truckId
            },
            exchange: "order-events",
            routingKey: "order.status.intransit");

        // Simulate travel time
        await Task.Delay(TimeSpan.FromSeconds(travelTimeSeconds), cancellationToken);

        // Publish arrived at job site event
        await _messagePublisher.PublishAsync(
            new ArrivedAtJobSiteEvent
            {
                TruckId = truckId.ToString(),
                JobSiteId = orderId.ToString()
            },
            exchange: "truck-events",
            routingKey: "truck.arrived.jobsite");

        _logger.LogInformation("Truck {TruckId}: Arrived at job site", truckId);
    }

    private async Task SimulateDeliveryAsync(
        int truckId,
        int orderId,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Starting delivery/pouring ({Time}s)", 
            truckId, DeliveryTimeSeconds);

        // Update status to Delivering
        await repository.UpdateTruckStatusAsync(truckId, "Delivering", orderId, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, "Delivering", cancellationToken);

        // Publish pouring started event
        await _messagePublisher.PublishAsync(
            new PouringStartedEvent
            {
                TruckId = truckId.ToString(),
                JobSiteId = orderId.ToString()
            },
            exchange: "truck-events",
            routingKey: "truck.pouring.started");

        // Simulate delivery time
        await Task.Delay(TimeSpan.FromSeconds(DeliveryTimeSeconds), cancellationToken);

        // Publish pouring completed event
        await _messagePublisher.PublishAsync(
            new PouringCompletedEvent
            {
                TruckId = truckId.ToString(),
                JobSiteId = orderId.ToString(),
                AmountPoured = 10 // cubic yards
            },
            exchange: "truck-events",
            routingKey: "truck.pouring.completed");

        _logger.LogInformation("Truck {TruckId}: Delivery complete", truckId);
    }

    private async Task SimulateTravelToYardAsync(
        int truckId,
        int orderId,
        int travelTimeSeconds,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Returning to yard ({Time}s)", 
            truckId, travelTimeSeconds);

        // Update status to Returning
        await repository.UpdateTruckStatusAsync(truckId, "Returning", orderId, cancellationToken);

        // Publish status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = "Delivering",
                NewStatus = "Returning"
            },
            exchange: "truck-events",
            routingKey: "truck.status.changed");

        // Simulate travel time
        await Task.Delay(TimeSpan.FromSeconds(travelTimeSeconds), cancellationToken);

        // Publish returned to plant event
        await _messagePublisher.PublishAsync(
            new ReturnedToPlantEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: "truck-events",
            routingKey: "truck.returned.plant");

        _logger.LogInformation("Truck {TruckId}: Returned to yard", truckId);
    }

    private async Task SimulateWashingAsync(
        int truckId,
        int orderId,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Starting wash ({Time}s)", 
            truckId, WashingTimeSeconds);

        // Update status to Washing
        await repository.UpdateTruckStatusAsync(truckId, "Washing", orderId, cancellationToken);

        // Publish wash started event
        await _messagePublisher.PublishAsync(
            new WashStartedEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: "truck-events",
            routingKey: "truck.wash.started");

        // Simulate wash time
        await Task.Delay(TimeSpan.FromSeconds(WashingTimeSeconds), cancellationToken);

        // Publish wash completed event
        await _messagePublisher.PublishAsync(
            new WashCompletedEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: "truck-events",
            routingKey: "truck.wash.completed");

        _logger.LogInformation("Truck {TruckId}: Wash complete", truckId);
    }

    private async Task CompleteWorkflowAsync(
        int truckId,
        int orderId,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Completing workflow", truckId);

        // Update status to Available (no order)
        await repository.UpdateTruckStatusAsync(truckId, "Available", null, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, "Delivered", cancellationToken);

        // Publish truck idle event
        await _messagePublisher.PublishAsync(
            new TruckIdleEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: "truck-events",
            routingKey: "truck.idle");

        // Publish order delivered event
        await _messagePublisher.PublishAsync(
            new OrderDeliveredEvent
            {
                OrderId = orderId,
                TruckId = truckId,
                DeliveredAt = DateTime.UtcNow
            },
            exchange: "order-events",
            routingKey: "order.delivered");

        _logger.LogInformation("Truck {TruckId}: Now available for new orders", truckId);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck Status Service stopping - cancelling all active truck simulations");

        // Cancel all active truck simulations
        foreach (var cts in _activeTrucks.Values)
        {
            cts.Cancel();
        }

        await base.StopAsync(cancellationToken);
    }
}
