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
    private readonly Random _random;

    // Simulation timing constants (in seconds)
    private const int BaseLoadingTimeSeconds = 15;
    private const int BaseDeliveryTimeSeconds = 15;
    private const int BaseWashingTimeSeconds = 10;
    private const int SecondsPerMile = 2;
    
    // Randomness ranges (in seconds)
    private const int LoadingVarianceSeconds = 5;      // ±5 seconds
    private const int DeliveryVarianceSeconds = 5;     // ±5 seconds
    private const int WashingVarianceSeconds = 3;      // ±3 seconds
    private const int TravelVariancePercent = 20;      // ±20% of calculated travel time

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
        _random = new Random();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Truck Status Service starting up");

        try
        {
            // Recover any trucks stuck in intermediate states
            await RecoverStuckTrucksAsync(stoppingToken);

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

            // Subscribe to order cancellation events
            await _messageConsumer.StartConsumingAsync<OrderCancelledEvent>(
                queueName: "truck-status-service-cancellations",
                handler: HandleOrderCancelledAsync,
                exchangeName: ExchangeNames.OrderEvents,
                routingKey: RoutingKeys.Order.Cancelled,
                cancellationToken: stoppingToken);

            _logger.LogInformation("Successfully subscribed to order cancellation events");

            // Start periodic check for assigned trucks (backup for missed messages)
            _ = Task.Run(async () => await PeriodicAssignedTrucksCheckAsync(stoppingToken), stoppingToken);

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

    private async Task RecoverStuckTrucksAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("RecoverStuckTrucks");
        
        try
        {
            _logger.LogInformation("Checking for trucks stuck in intermediate states on startup...");

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<TruckStatusRepository>();

            var stuckTrucks = await repository.GetTrucksInIntermediateStatesAsync(cancellationToken);

            if (stuckTrucks.Count == 0)
            {
                _logger.LogInformation("No stuck trucks found");
                return;
            }

            _logger.LogWarning(
                "Found {Count} truck(s) stuck in intermediate states - recovering them",
                stuckTrucks.Count);

            foreach (var (truckId, orderId, status, driverName) in stuckTrucks)
            {
                _logger.LogWarning(
                    "Recovering truck {TruckId} (driver: {DriverName}) stuck in {Status} state (order: {OrderId})",
                    truckId, driverName, status, orderId);

                if (orderId.HasValue)
                {
                    // Truck has an order - resume workflow from current state
                    await ResumeWorkflowFromStateAsync(truckId, orderId.Value, status, cancellationToken);
                }
                else
                {
                    // Truck has no order but is in intermediate state - force to Available
                    _logger.LogWarning(
                        "Truck {TruckId} is in {Status} state but has no order - forcing to Available",
                        truckId, status);
                    
                    await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Available, null, cancellationToken);
                    
                    await _messagePublisher.PublishAsync(
                        new TruckStatusChangedEvent
                        {
                            TruckId = truckId.ToString(),
                            PreviousStatus = status,
                            NewStatus = TruckStatus.Available
                        },
                        exchange: ExchangeNames.TruckEvents,
                        routingKey: RoutingKeys.Truck.StatusChanged);
                }
            }

            _logger.LogInformation(
                "Recovery complete - processed {Count} stuck truck(s)",
                stuckTrucks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recovering stuck trucks on startup");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't throw - allow service to continue even if recovery fails
        }
    }

    private async Task ResumeWorkflowFromStateAsync(
        int truckId,
        int orderId,
        string currentState,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Resuming workflow for truck {TruckId} from {CurrentState}",
            truckId, currentState);

        // Create a cancellation token for this specific truck simulation
        var truckCts = new CancellationTokenSource();
        _activeTrucks[truckId] = truckCts;

        // Start the simulation in a background task, picking up from current state
        _ = Task.Run(async () =>
        {
            try
            {
                await ContinueWorkflowFromStateAsync(truckId, orderId, currentState, truckCts.Token);
            }
            finally
            {
                _activeTrucks.TryRemove(truckId, out _);
                truckCts.Dispose();
            }
        }, truckCts.Token);
    }

    private async Task ContinueWorkflowFromStateAsync(
        int truckId,
        int orderId,
        string currentState,
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("ContinueWorkflowFromState");
        activity?.SetTag("truck.id", truckId);
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("resume.state", currentState);

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TruckStatusRepository>();

        try
        {
            // Get order details to determine travel time
            var order = await repository.GetOrderForTruckAsync(truckId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("No order found for truck {TruckId} - cannot resume workflow", truckId);
                return;
            }

            // Calculate randomized times for recovery
            var deliveryTime = CalculateDeliveryTime();
            var washingTime = CalculateWashingTime();
            var travelTimeToSite = CalculateTravelTime(order.DistanceMiles);
            var travelTimeToYard = CalculateTravelTime(order.DistanceMiles);

            _logger.LogInformation(
                "Resuming workflow for truck {TruckId} from {CurrentState}",
                truckId, currentState);

            // Resume from the appropriate state
            // For simplicity in demo, we'll fast-forward to the next logical state
            switch (currentState)
            {
                case TruckStatus.Loading:
                    // Skip to travel phase
                    await SimulateTravelToSiteAsync(truckId, orderId, travelTimeToSite, repository, cancellationToken);
                    await SimulateDeliveryAsync(truckId, orderId, deliveryTime, repository, cancellationToken);
                    await SimulateTravelToYardAsync(truckId, orderId, travelTimeToYard, repository, cancellationToken);
                    await SimulateWashingAsync(truckId, orderId, washingTime, repository, cancellationToken);
                    break;

                case TruckStatus.EnRoute:
                    // Skip to delivery
                    await SimulateDeliveryAsync(truckId, orderId, deliveryTime, repository, cancellationToken);
                    await SimulateTravelToYardAsync(truckId, orderId, travelTimeToYard, repository, cancellationToken);
                    await SimulateWashingAsync(truckId, orderId, washingTime, repository, cancellationToken);
                    break;

                case TruckStatus.AtJobSite:
                case TruckStatus.Delivering:
                    // Skip to return
                    await SimulateTravelToYardAsync(truckId, orderId, travelTimeToYard, repository, cancellationToken);
                    await SimulateWashingAsync(truckId, orderId, washingTime, repository, cancellationToken);
                    break;

                case TruckStatus.Returning:
                    // Just do washing
                    await SimulateWashingAsync(truckId, orderId, washingTime, repository, cancellationToken);
                    break;

                case TruckStatus.Washing:
                    // Almost done - just a few more seconds
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    await _messagePublisher.PublishAsync(
                        new WashCompletedEvent
                        {
                            TruckId = truckId.ToString()
                        },
                        exchange: ExchangeNames.TruckEvents,
                        routingKey: RoutingKeys.Truck.WashCompleted);
                    break;
            }

            // Complete the workflow
            await CompleteWorkflowAsync(truckId, orderId, repository, cancellationToken);

            _logger.LogInformation(
                "Completed recovered workflow for truck {TruckId}",
                truckId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Recovered workflow cancelled for truck {TruckId}",
                truckId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in recovered workflow for truck {TruckId}",
                truckId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
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
                // Skip if truck workflow is already active
                if (_activeTrucks.ContainsKey(truckId))
                {
                    _logger.LogDebug(
                        "Truck {TruckId} workflow already active - skipping",
                        truckId);
                    continue;
                }

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

    private async Task HandleOrderCancelledAsync(OrderCancelledEvent cancelEvent)
    {
        using var activity = _activitySource.StartActivity("HandleOrderCancelled");
        activity?.SetTag("order.id", cancelEvent.OrderId);

        try
        {
            _logger.LogInformation(
                "Order {OrderId} cancelled - checking for assigned truck",
                cancelEvent.OrderId);

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<TruckStatusRepository>();

            // Find which truck (if any) was assigned to this order
            var truckStatus = await repository.GetTruckByOrderIdAsync(cancelEvent.OrderId, CancellationToken.None);
            
            if (truckStatus == null)
            {
                _logger.LogInformation(
                    "No truck found for cancelled order {OrderId}",
                    cancelEvent.OrderId);
                return;
            }

            var (truckId, currentStatus) = truckStatus.Value;

            _logger.LogInformation(
                "Truck {TruckId} was assigned to cancelled order {OrderId} (current status: {Status})",
                truckId, cancelEvent.OrderId, currentStatus);

            // Cancel the active workflow for this truck if it's running
            if (_activeTrucks.TryRemove(truckId, out var cts))
            {
                _logger.LogInformation("Cancelling active workflow for truck {TruckId}", truckId);
                cts.Cancel();
                cts.Dispose();
            }

            // Check current status and determine action
            if (currentStatus == TruckStatus.Available)
            {
                _logger.LogInformation(
                    "Truck {TruckId} is already available - no action needed",
                    truckId);
            }
            else if (currentStatus == TruckStatus.Washing || currentStatus == TruckStatus.Returning)
            {
                _logger.LogInformation(
                    "Truck {TruckId} is already returning/washing - no action needed",
                    truckId);
            }
            else
            {
                // Any other status (Assigned, Loading, EnRoute, Delivering, AtJobSite)
                // needs to go through return-to-plant workflow
                _logger.LogInformation(
                    "Truck {TruckId} needs to return to plant from status {Status}",
                    truckId, currentStatus);

                // Start a return-to-plant workflow
                var truckCts = new CancellationTokenSource();
                _activeTrucks[truckId] = truckCts;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SimulateReturnFromCancelledOrderAsync(truckId, currentStatus, repository, truckCts.Token);
                    }
                    finally
                    {
                        _activeTrucks.TryRemove(truckId, out _);
                        truckCts.Dispose();
                    }
                }, truckCts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling order cancellation for order {OrderId}",
                cancelEvent.OrderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    private async Task SimulateReturnFromCancelledOrderAsync(
        int truckId,
        string currentStatus,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("SimulateReturnFromCancelledOrder");
        activity?.SetTag("truck.id", truckId);
        activity?.SetTag("previous.status", currentStatus);

        try
        {
            _logger.LogInformation(
                "Starting return workflow for truck {TruckId} from cancelled order (was in {Status})",
                truckId, currentStatus);

            // Get a rough distance estimate for return (use 20 miles as average)
            var estimatedReturnTime = CalculateTravelTime(20);
            var washTime = CalculateWashingTime();

            // If truck was still at plant (Assigned or Loading), it goes directly to washing
            // Otherwise, it needs to return from the job site first
            if (currentStatus == TruckStatus.Assigned || currentStatus == TruckStatus.Loading)
            {
                _logger.LogInformation(
                    "Truck {TruckId} was still at plant - skipping return trip, going directly to washing",
                    truckId);

                // Skip the return trip - truck never left the plant
                // Go straight to washing
            }
            else
            {
                // Truck was en route or at job site - needs to return
                await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Returning, null, cancellationToken);

                await _messagePublisher.PublishAsync(
                    new TruckStatusChangedEvent
                    {
                        TruckId = truckId.ToString(),
                        PreviousStatus = currentStatus,
                        NewStatus = TruckStatus.Returning
                    },
                    exchange: ExchangeNames.TruckEvents,
                    routingKey: RoutingKeys.Truck.StatusChanged);

                _logger.LogInformation(
                    "Truck {TruckId}: Returning to plant ({Time}s)",
                    truckId, estimatedReturnTime);

                // Simulate return time
                await Task.Delay(TimeSpan.FromSeconds(estimatedReturnTime), cancellationToken);

                await _messagePublisher.PublishAsync(
                    new ReturnedToPlantEvent
                    {
                        TruckId = truckId.ToString()
                    },
                    exchange: ExchangeNames.TruckEvents,
                    routingKey: RoutingKeys.Truck.ReturnedToPlant);
            }

            // Now wash the truck
            await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Washing, null, cancellationToken);

            await _messagePublisher.PublishAsync(
                new TruckStatusChangedEvent
                {
                    TruckId = truckId.ToString(),
                    PreviousStatus = (currentStatus == TruckStatus.Assigned || currentStatus == TruckStatus.Loading) ? currentStatus : TruckStatus.Returning,
                    NewStatus = TruckStatus.Washing
                },
                exchange: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.StatusChanged);

            await _messagePublisher.PublishAsync(
                new WashStartedEvent
                {
                    TruckId = truckId.ToString()
                },
                exchange: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.WashStarted);

            _logger.LogInformation("Truck {TruckId}: Starting wash ({Time}s)", truckId, washTime);

            await Task.Delay(TimeSpan.FromSeconds(washTime), cancellationToken);

            await _messagePublisher.PublishAsync(
                new WashCompletedEvent
                {
                    TruckId = truckId.ToString()
                },
                exchange: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.WashCompleted);

            // Finally, make truck available
            await repository.UpdateTruckStatusAsync(truckId, TruckStatus.Available, null, cancellationToken);

            await _messagePublisher.PublishAsync(
                new TruckStatusChangedEvent
                {
                    TruckId = truckId.ToString(),
                    PreviousStatus = TruckStatus.Washing,
                    NewStatus = TruckStatus.Available
                },
                exchange: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.StatusChanged);

            await _messagePublisher.PublishAsync(
                new TruckIdleEvent
                {
                    TruckId = truckId.ToString()
                },
                exchange: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.Idle);

            _logger.LogInformation(
                "Truck {TruckId} completed return from cancelled order and is now available",
                truckId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Return workflow cancelled for truck {TruckId}",
                truckId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in return workflow for truck {TruckId}",
                truckId);
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

            // Calculate randomized times
            var loadingTime = CalculateLoadingTime();
            var deliveryTime = CalculateDeliveryTime();
            var washingTime = CalculateWashingTime();
            var travelTimeToSite = CalculateTravelTime(order.DistanceMiles);
            var travelTimeToYard = CalculateTravelTime(order.DistanceMiles);
            var totalTime = loadingTime + travelTimeToSite + deliveryTime + 
                           travelTimeToYard + washingTime;

            _logger.LogInformation(
                "Starting workflow for truck {TruckId} - Total estimated time: {TotalTime}s " +
                "(Loading: {Loading}s, Travel to site: {TravelTo}s, Delivery: {Delivery}s, " +
                "Travel to yard: {TravelBack}s, Wash: {Wash}s)",
                truckId, totalTime, loadingTime, travelTimeToSite, deliveryTime,
                travelTimeToYard, washingTime);

            // Phase 1: Loading
            await SimulateLoadingAsync(truckId, orderId, loadingTime, repository, cancellationToken);

            // Phase 2: Travel to job site
            await SimulateTravelToSiteAsync(truckId, orderId, travelTimeToSite, repository, cancellationToken);

            // Phase 3: Delivery/Pouring
            await SimulateDeliveryAsync(truckId, orderId, deliveryTime, repository, cancellationToken);

            // Phase 4: Travel back to yard
            await SimulateTravelToYardAsync(truckId, orderId, travelTimeToYard, repository, cancellationToken);

            // Phase 5: Washing
            await SimulateWashingAsync(truckId, orderId, washingTime, repository, cancellationToken);

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
        int loadingTimeSeconds,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Starting loading phase ({Time}s)", 
            truckId, loadingTimeSeconds);

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
        await Task.Delay(TimeSpan.FromSeconds(loadingTimeSeconds), cancellationToken);

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
        int deliveryTimeSeconds,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Starting delivery/pouring ({Time}s)", 
            truckId, deliveryTimeSeconds);

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
        await Task.Delay(TimeSpan.FromSeconds(deliveryTimeSeconds), cancellationToken);

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
        int washingTimeSeconds,
        TruckStatusRepository repository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truck {TruckId}: Starting wash ({Time}s)", 
            truckId, washingTimeSeconds);

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
        await Task.Delay(TimeSpan.FromSeconds(washingTimeSeconds), cancellationToken);

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
            routingKey: RoutingKeys.Truck.Idle);

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

    /// <summary>
    /// Calculate randomized loading time
    /// </summary>
    private int CalculateLoadingTime()
    {
        var variance = _random.Next(-LoadingVarianceSeconds, LoadingVarianceSeconds + 1);
        return Math.Max(5, BaseLoadingTimeSeconds + variance); // Minimum 5 seconds
    }

    /// <summary>
    /// Calculate randomized delivery time
    /// </summary>
    private int CalculateDeliveryTime()
    {
        var variance = _random.Next(-DeliveryVarianceSeconds, DeliveryVarianceSeconds + 1);
        return Math.Max(5, BaseDeliveryTimeSeconds + variance); // Minimum 5 seconds
    }

    /// <summary>
    /// Calculate randomized washing time
    /// </summary>
    private int CalculateWashingTime()
    {
        var variance = _random.Next(-WashingVarianceSeconds, WashingVarianceSeconds + 1);
        return Math.Max(3, BaseWashingTimeSeconds + variance); // Minimum 3 seconds
    }

    /// <summary>
    /// Calculate randomized travel time based on distance
    /// </summary>
    private int CalculateTravelTime(int distanceMiles)
    {
        var baseTime = distanceMiles * SecondsPerMile;
        var variancePercent = _random.Next(-TravelVariancePercent, TravelVariancePercent + 1);
        var variance = (baseTime * variancePercent) / 100;
        return Math.Max(2, baseTime + variance); // Minimum 2 seconds
    }

    /// <summary>
    /// Periodically check for assigned trucks that haven't started (backup for missed messages)
    /// </summary>
    private async Task PeriodicAssignedTrucksCheckAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting periodic assigned trucks check (every 30 seconds)");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                try
                {
                    await ProcessAssignedTrucksAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in periodic assigned trucks check");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Periodic assigned trucks check cancelled");
        }
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
