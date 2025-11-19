using System.Diagnostics;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.JobWorkflowService.Services;

/// <summary>
/// Background service that watches for new orders and assigns idle trucks
/// </summary>
public class JobWorkflowOrchestrator : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobWorkflowOrchestrator> _logger;
    private readonly ActivitySource _activitySource;

    public JobWorkflowOrchestrator(
        IMessageConsumer messageConsumer,
        IMessagePublisher messagePublisher,
        IServiceProvider serviceProvider,
        ILogger<JobWorkflowOrchestrator> logger)
    {
        _messageConsumer = messageConsumer;
        _messagePublisher = messagePublisher;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.JobWorkflowService");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Workflow Orchestrator starting up");

        try
        {
            // Process any existing pending orders on startup
            await ProcessPendingOrdersAsync(stoppingToken);

            // Subscribe to order created events
            await _messageConsumer.StartConsumingAsync<OrderCreatedEvent>(
                queueName: "job-workflow-service-orders",
                handler: HandleOrderCreatedAsync,
                exchangeName: "order-events",
                routingKey: "order.created",
                cancellationToken: stoppingToken);

            _logger.LogInformation("Successfully subscribed to order created events");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job Workflow Orchestrator is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Job Workflow Orchestrator");
            throw;
        }
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("ProcessPendingOrders");
        
        try
        {
            _logger.LogInformation("Checking for pending orders on startup...");

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<JobWorkflowRepository>();

            var pendingOrders = await repository.GetPendingOrdersAsync(cancellationToken);

            if (pendingOrders.Count == 0)
            {
                _logger.LogInformation("No pending orders found");
                return;
            }

            _logger.LogInformation("Found {Count} pending order(s) - attempting to assign trucks", pendingOrders.Count);

            int assignedCount = 0;
            foreach (var order in pendingOrders)
            {
                // Check if we still have available trucks
                var availableTruck = await repository.GetAvailableTruckAsync(cancellationToken);

                if (availableTruck == null)
                {
                    _logger.LogWarning(
                        "No more available trucks - {RemainingCount} order(s) will remain pending",
                        pendingOrders.Count - assignedCount);
                    break;
                }

                var (truckId, driverName) = availableTruck.Value;

                _logger.LogInformation(
                    "Assigning truck {TruckId} (driver: {DriverName}) to pending order {OrderId}",
                    truckId, driverName, order.Id);

                // Assign truck to order in database
                await repository.AssignTruckToOrderAsync(order.Id, truckId, cancellationToken);

                // Publish truck assigned event
                await _messagePublisher.PublishAsync(
                    new TruckAssignedToOrderEvent
                    {
                        OrderId = order.Id,
                        TruckId = truckId,
                        TruckDriverName = driverName
                    },
                    exchange: "order-events",
                    routingKey: "order.truck.assigned");

                assignedCount++;
            }

            _logger.LogInformation(
                "Startup processing complete - assigned trucks to {AssignedCount} of {TotalCount} pending order(s)",
                assignedCount, pendingOrders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending orders on startup");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't throw - allow service to continue even if startup processing fails
        }
    }

    private async Task HandleOrderCreatedAsync(OrderCreatedEvent orderEvent)
    {
        using var activity = _activitySource.StartActivity("HandleOrderCreated");
        activity?.SetTag("order.id", orderEvent.OrderId);
        activity?.SetTag("customer.name", orderEvent.CustomerName);

        try
        {
            _logger.LogInformation(
                "New order received: {OrderId} for customer {CustomerName} - Distance: {Distance} miles",
                orderEvent.OrderId, orderEvent.CustomerName, orderEvent.DistanceMiles);

            // Find an available truck
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<JobWorkflowRepository>();

            var availableTruck = await repository.GetAvailableTruckAsync(CancellationToken.None);

            if (availableTruck == null)
            {
                _logger.LogWarning(
                    "No available trucks for order {OrderId} - order will remain in Pending status",
                    orderEvent.OrderId);
                
                // TODO: Could implement a queue or retry mechanism here
                return;
            }

            var (truckId, driverName) = availableTruck.Value;

            _logger.LogInformation(
                "Assigning truck {TruckId} (driver: {DriverName}) to order {OrderId}",
                truckId, driverName, orderEvent.OrderId);

            // Assign truck to order in database
            await repository.AssignTruckToOrderAsync(orderEvent.OrderId, truckId, CancellationToken.None);

            // Publish truck assigned event
            await _messagePublisher.PublishAsync(
                new TruckAssignedToOrderEvent
                {
                    OrderId = orderEvent.OrderId,
                    TruckId = truckId,
                    TruckDriverName = driverName
                },
                exchange: "order-events",
                routingKey: "order.truck.assigned");

            _logger.LogInformation(
                "Successfully assigned truck {TruckId} to order {OrderId}",
                truckId, orderEvent.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling order created event for order {OrderId}",
                orderEvent.OrderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Job Workflow Orchestrator stopping");
        return base.StopAsync(cancellationToken);
    }
}
