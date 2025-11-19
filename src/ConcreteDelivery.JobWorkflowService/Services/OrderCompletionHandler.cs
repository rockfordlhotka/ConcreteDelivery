using System.Diagnostics;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Constants;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.JobWorkflowService.Services;

/// <summary>
/// Background service that handles order completion and marks orders as delivered
/// </summary>
public class OrderCompletionHandler : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderCompletionHandler> _logger;
    private readonly ActivitySource _activitySource;

    public OrderCompletionHandler(
        IMessageConsumer messageConsumer,
        IServiceProvider serviceProvider,
        ILogger<OrderCompletionHandler> logger)
    {
        _messageConsumer = messageConsumer;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.JobWorkflowService");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Completion Handler starting up - listening for order deliveries");

        try
        {
            // Subscribe to order delivered events
            await _messageConsumer.StartConsumingAsync<OrderDeliveredEvent>(
                queueName: "job-workflow-service-completions",
                handler: HandleOrderDeliveredAsync,
                exchangeName: ExchangeNames.OrderEvents,
                routingKey: RoutingKeys.Order.Delivered,
                cancellationToken: stoppingToken);

            _logger.LogInformation("Successfully subscribed to order delivered events");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Order Completion Handler is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Order Completion Handler");
            throw;
        }
    }

    private async Task HandleOrderDeliveredAsync(OrderDeliveredEvent deliveredEvent)
    {
        using var activity = _activitySource.StartActivity("HandleOrderDelivered");
        activity?.SetTag("order.id", deliveredEvent.OrderId);
        activity?.SetTag("truck.id", deliveredEvent.TruckId);

        try
        {
            _logger.LogInformation(
                "Order {OrderId} delivered by truck {TruckId} at {DeliveredAt}",
                deliveredEvent.OrderId, deliveredEvent.TruckId, deliveredEvent.DeliveredAt);

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<JobWorkflowRepository>();

            // Update order status to "Delivered"
            await repository.UpdateOrderStatusAsync(deliveredEvent.OrderId, "Delivered");

            _logger.LogInformation(
                "Successfully marked order {OrderId} as completed",
                deliveredEvent.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling order delivered event for order {OrderId}",
                deliveredEvent.OrderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order Completion Handler stopping");
        return base.StopAsync(cancellationToken);
    }
}
