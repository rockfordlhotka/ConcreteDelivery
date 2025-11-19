using System.Collections.Concurrent;
using System.Diagnostics;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Constants;
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
        _logger.LogInformation("Truck Status Service starting up");

        try
        {
            // Process any trucks that are already assigned to orders on startup
            await ProcessAssignedTrucksAsync(stoppingToken);

            // Subscribe to truck assignment events
            await _messageConsumer.StartConsumingAsync<TruckAssignedToOrderEvent>(
                queueName: "truck-status-service-assignments",
                handler: HandleTruckAssignmentAsync,
                exchangeName: ExchangeNames.OrderEvents,
                routingKey: RoutingKeys.Order.TruckAssigned,
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

    private async Task ProcessAssignedTrucksAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("ProcessAssignedTrucks");
        
        try
        {
            _logger.LogInformation("Checking for trucks already assigned to orders on startup...");

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<TruckStatusRepository>();

            var assignedTrucks = await repository.GetAssignedTrucksAsync(cancellationToken);

            if (assignedTrucks.Count == 0)
            {
                _logger.LogInformation("No assigned trucks found");
                return;
            }

            _logger.LogInformation(
                "Found {Count} assigned truck(s) - starting their workflows",
                assignedTrucks.Count);

            foreach (var (truckId, orderId, driverName) in assignedTrucks)
            {
                _logger.LogInformation(
                    "Starting workflow for truck {TruckId} (driver: {DriverName}) with order {OrderId}",
                    truckId, driverName, orderId);

                // Create a cancellation token for this specific truck simulation
                var truckCts = new CancellationTokenSource();
                _activeTrucks[truckId] = truckCts;

                // Start the simulation in a background task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SimulateTruckWorkflowAsync(truckId, orderId, truckCts.Token);
                    }
                    finally
                    {
                        _activeTrucks.TryRemove(truckId, out _);
                        truckCts.Dispose();
                    }
                }, truckCts.Token);
            }

            _logger.LogInformation(
                "Startup processing complete - started workflows for {Count} truck(s)",
                assignedTrucks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing assigned trucks on startup");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't throw - allow service to continue even if startup processing fails
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
        await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Loading, orderId, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, TruckStatus.Loading, cancellationToken);

        // Publish truck status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = TruckStatus.Available,
                NewStatus = TruckStatus.Loading
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.StatusChanged);

        // Simulate loading time
        await Task.Delay(TimeSpan.FromSeconds(LoadingTimeSeconds), cancellationToken);

        // Publish materials loaded event
        await _messagePublisher.PublishAsync(
            new MaterialsLoadedEvent
            {
                TruckId = truckId.ToString(),
                Materials = new Dictionary<string, decimal>
                {
                    { "Concrete", 10 } // cubic yards
                }
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.MaterialsLoaded);

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
        await repository.UpdateTruckStatusAsync(truckId, TruckStatus.EnRoute, orderId, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, "InTransit", cancellationToken);

        // Publish status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = TruckStatus.Loading,
                NewStatus = TruckStatus.EnRoute
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.StatusChanged);

        // Publish order in transit event
        await _messagePublisher.PublishAsync(
            new OrderInTransitEvent
            {
                OrderId = orderId,
                TruckId = truckId
            },
            exchange: ExchangeNames.OrderEvents,
            routingKey: RoutingKeys.Order.InTransit);

        // Simulate travel time
        await Task.Delay(TimeSpan.FromSeconds(travelTimeSeconds), cancellationToken);

        // Publish arrived at job site event
        await _messagePublisher.PublishAsync(
            new ArrivedAtJobSiteEvent
            {
                TruckId = truckId.ToString(),
                JobSiteId = orderId.ToString()
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.ArrivedAtJobSite);

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
        await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Delivering, orderId, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, TruckStatus.Delivering, cancellationToken);

        // Publish status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = TruckStatus.EnRoute,
                NewStatus = TruckStatus.Delivering
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.StatusChanged);

        // Publish pouring started event
        await _messagePublisher.PublishAsync(
            new PouringStartedEvent
            {
                TruckId = truckId.ToString(),
                JobSiteId = orderId.ToString()
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.PouringStarted);

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
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.PouringCompleted);

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
        await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Returning, orderId, cancellationToken);

        // Publish status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = TruckStatus.Delivering,
                NewStatus = TruckStatus.Returning
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.StatusChanged);

        // Simulate travel time
        await Task.Delay(TimeSpan.FromSeconds(travelTimeSeconds), cancellationToken);

        // Publish returned to plant event
        await _messagePublisher.PublishAsync(
            new ReturnedToPlantEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.ReturnedToPlant);

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
        await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Washing, orderId, cancellationToken);

        // Publish status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = TruckStatus.Returning,
                NewStatus = TruckStatus.Washing
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.StatusChanged);

        // Publish wash started event
        await _messagePublisher.PublishAsync(
            new WashStartedEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.WashStarted);

        // Simulate wash time
        await Task.Delay(TimeSpan.FromSeconds(WashingTimeSeconds), cancellationToken);

        // Publish wash completed event
        await _messagePublisher.PublishAsync(
            new WashCompletedEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.WashCompleted);

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
        await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Available, null, cancellationToken);
        await repository.UpdateOrderStatusAsync(orderId, "Delivered", cancellationToken);

        // Publish status changed event
        await _messagePublisher.PublishAsync(
            new TruckStatusChangedEvent
            {
                TruckId = truckId.ToString(),
                PreviousStatus = TruckStatus.Washing,
                NewStatus = TruckStatus.Available
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: RoutingKeys.Truck.StatusChanged);

        // Publish truck idle event
        await _messagePublisher.PublishAsync(
            new TruckIdleEvent
            {
                TruckId = truckId.ToString()
            },
            exchange: ExchangeNames.TruckEvents,
            routingKey: "truck.idle");

        // Publish order delivered event
        await _messagePublisher.PublishAsync(
            new OrderDeliveredEvent
            {
                OrderId = orderId,
                TruckId = truckId,
                DeliveredAt = DateTime.UtcNow
            },
            exchange: ExchangeNames.OrderEvents,
            routingKey: RoutingKeys.Order.Delivered);

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
