using System.Diagnostics;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Constants;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.JobWorkflowService.Services;

/// <summary>
/// Background service that handles order cancellations and returns trucks to plant
/// </summary>
public class OrderCancellationHandler : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderCancellationHandler> _logger;
    private readonly ActivitySource _activitySource;

    public OrderCancellationHandler(
        IMessageConsumer messageConsumer,
        IMessagePublisher messagePublisher,
        IServiceProvider serviceProvider,
        ILogger<OrderCancellationHandler> logger)
    {
        _messageConsumer = messageConsumer;
        _messagePublisher = messagePublisher;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.JobWorkflowService");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Cancellation Handler starting up - listening for cancelled orders");

        try
        {
            // Subscribe to order cancelled events
            await _messageConsumer.StartConsumingAsync<OrderCancelledEvent>(
                queueName: "job-workflow-service-cancellations",
                handler: HandleOrderCancelledAsync,
                exchangeName: ExchangeNames.OrderEvents,
                routingKey: RoutingKeys.Order.Cancelled,
                cancellationToken: stoppingToken);

            _logger.LogInformation("Successfully subscribed to order cancelled events");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Order Cancellation Handler is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Order Cancellation Handler");
            throw;
        }
    }

    private async Task HandleOrderCancelledAsync(OrderCancelledEvent cancelEvent)
    {
        using var activity = _activitySource.StartActivity("HandleOrderCancelled");
        activity?.SetTag("order.id", cancelEvent.OrderId);
        activity?.SetTag("reason", cancelEvent.Reason);

        try
        {
            _logger.LogInformation(
                "Order {OrderId} cancelled - Reason: {Reason}",
                cancelEvent.OrderId, cancelEvent.Reason);

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<JobWorkflowRepository>();

            // Get the truck assigned to this order
            var truckId = await repository.GetTruckIdForOrderAsync(cancelEvent.OrderId);

            if (truckId == null)
            {
                _logger.LogWarning(
                    "No truck assigned to cancelled order {OrderId}",
                    cancelEvent.OrderId);
                return;
            }

            _logger.LogInformation(
                "Sending truck {TruckId} back to plant due to order cancellation",
                truckId);

            // Update truck status to "Returning"
            await repository.UpdateTruckStatusAsync(truckId.Value, "Returning", null);

            // Publish truck status change event
            await _messagePublisher.PublishAsync(
                new TruckStatusChangedEvent
                {
                    TruckId = truckId.Value.ToString(),
                    PreviousStatus = "Assigned",
                    NewStatus = "Returning"
                },
                exchange: ExchangeNames.TruckEvents,
                routingKey: RoutingKeys.Truck.StatusChanged);

            // Publish return to plant command
            await _messagePublisher.PublishAsync(
                new ReturnToPlantCommand
                {
                    TruckId = truckId.Value.ToString()
                },
                exchange: ExchangeNames.TruckCommands,
                routingKey: $"truck.{truckId}.returntoplant");

            _logger.LogInformation(
                "Successfully handled cancellation of order {OrderId} - Truck {TruckId} returning to plant",
                cancelEvent.OrderId, truckId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling order cancelled event for order {OrderId}",
                cancelEvent.OrderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order Cancellation Handler stopping");
        return base.StopAsync(cancellationToken);
    }
}
