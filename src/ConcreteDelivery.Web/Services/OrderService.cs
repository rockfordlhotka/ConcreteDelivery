using ConcreteDelivery.Data;
using ConcreteDelivery.Data.Entities;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Constants;
using ConcreteDelivery.Messaging.Messages;
using Microsoft.EntityFrameworkCore;

namespace ConcreteDelivery.Web.Services;

/// <summary>
/// Service for managing orders and publishing order-related messages to RabbitMQ
/// </summary>
public class OrderService
{
    private readonly IDbContextFactory<ConcreteDeliveryDbContext> _contextFactory;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IDbContextFactory<ConcreteDeliveryDbContext> contextFactory,
        IMessagePublisher messagePublisher,
        ILogger<OrderService> logger)
    {
        _contextFactory = contextFactory;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <summary>
    /// Get all orders with related data
    /// </summary>
    public async Task<List<Order>> GetAllOrdersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Orders
            .Include(o => o.Plant)
            .Include(o => o.Truck)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific order by ID with related data
    /// </summary>
    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Orders
            .Include(o => o.Plant)
            .Include(o => o.Truck)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    /// <summary>
    /// Get all plants for dropdown selection
    /// </summary>
    public async Task<List<Plant>> GetPlantsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Plants.ToListAsync();
    }

    /// <summary>
    /// Get all available trucks for dropdown selection
    /// </summary>
    public async Task<List<Truck>> GetAvailableTrucksAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Trucks
            .Include(t => t.CurrentStatus)
            .Where(t => t.CurrentStatus != null && t.CurrentStatus.Status == Messaging.Constants.TruckStatus.Available)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new order and publish OrderCreatedEvent
    /// </summary>
    public async Task<Order> CreateOrderAsync(string customerName, int distanceMiles, int? plantId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var order = new Order
        {
            CustomerName = customerName,
            DistanceMiles = distanceMiles,
            PlantId = plantId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Reload with navigation properties
        await context.Entry(order).Reference(o => o.Plant).LoadAsync();

        // Publish event
        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerName = order.CustomerName,
            DistanceMiles = order.DistanceMiles,
            PlantId = order.PlantId,
            Status = order.Status
        };

        await _messagePublisher.PublishAsync(orderCreatedEvent, ExchangeNames.OrderEvents, RoutingKeys.Order.Created);
        
        _logger.LogInformation("Order {OrderId} created for customer {CustomerName}", order.Id, order.CustomerName);

        return order;
    }

    /// <summary>
    /// Update an existing order and publish OrderUpdatedEvent
    /// </summary>
    public async Task<Order?> UpdateOrderAsync(int id, string customerName, int distanceMiles, int? plantId = null, int? truckId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var order = await context.Orders.FindAsync(id);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for update", id);
            return null;
        }

        // Only allow updates if order is Pending or Assigned
        if (order.Status != "Pending" && order.Status != "Assigned")
        {
            _logger.LogWarning("Cannot update order {OrderId} with status {Status}", id, order.Status);
            throw new InvalidOperationException($"Cannot update order with status {order.Status}");
        }

        order.CustomerName = customerName;
        order.DistanceMiles = distanceMiles;
        order.PlantId = plantId;
        order.TruckId = truckId;
        order.UpdatedAt = DateTime.UtcNow;

        // Update status if truck is assigned
        if (truckId.HasValue && order.Status == "Pending")
        {
            order.Status = "Assigned";
        }

        await context.SaveChangesAsync();

        // Reload with navigation properties
        await context.Entry(order).Reference(o => o.Plant).LoadAsync();
        await context.Entry(order).Reference(o => o.Truck).LoadAsync();

        // Publish event
        var orderUpdatedEvent = new OrderUpdatedEvent
        {
            OrderId = order.Id,
            CustomerName = order.CustomerName,
            DistanceMiles = order.DistanceMiles,
            PlantId = order.PlantId,
            TruckId = order.TruckId,
            Status = order.Status
        };

        await _messagePublisher.PublishAsync(orderUpdatedEvent, ExchangeNames.OrderEvents, RoutingKeys.Order.Updated);
        
        _logger.LogInformation("Order {OrderId} updated", order.Id);

        return order;
    }

    /// <summary>
    /// Cancel an order and publish OrderCancelledEvent
    /// </summary>
    public async Task<bool> CancelOrderAsync(int id, string reason = "Customer request")
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var order = await context.Orders.FindAsync(id);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for cancellation", id);
            return false;
        }

        // Only allow cancellation if order is not already Delivered or Cancelled
        if (order.Status == "Delivered" || order.Status == "Cancelled")
        {
            _logger.LogWarning("Cannot cancel order {OrderId} with status {Status}", id, order.Status);
            return false;
        }

        var previousStatus = order.Status;
        order.Status = "Cancelled";
        order.UpdatedAt = DateTime.UtcNow;

        // If order had a truck assigned, clear it
        if (order.TruckId.HasValue)
        {
            order.TruckId = null;
        }

        await context.SaveChangesAsync();

        // Publish cancellation event
        var orderCancelledEvent = new OrderCancelledEvent
        {
            OrderId = order.Id,
            CustomerName = order.CustomerName,
            Reason = reason
        };

        await _messagePublisher.PublishAsync(orderCancelledEvent, ExchangeNames.OrderEvents, RoutingKeys.Order.Cancelled);

        // Also publish status changed event
        var statusChangedEvent = new OrderStatusChangedEvent
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = "Cancelled"
        };

        await _messagePublisher.PublishAsync(statusChangedEvent, ExchangeNames.OrderEvents, RoutingKeys.Order.StatusChanged);
        
        _logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", order.Id, reason);

        return true;
    }

    /// <summary>
    /// Assign a truck to an order
    /// </summary>
    public async Task<bool> AssignTruckToOrderAsync(int orderId, int truckId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var order = await context.Orders.FindAsync(orderId);
        if (order == null || order.Status != "Pending")
        {
            return false;
        }

        var truck = await context.Trucks.FindAsync(truckId);
        if (truck == null)
        {
            return false;
        }

        order.TruckId = truckId;
        order.Status = "Assigned";
        order.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // Publish event
        var truckAssignedEvent = new TruckAssignedToOrderEvent
        {
            OrderId = order.Id,
            TruckId = truckId,
            TruckDriverName = truck.DriverName
        };

        await _messagePublisher.PublishAsync(truckAssignedEvent, ExchangeNames.OrderEvents, RoutingKeys.Order.TruckAssigned);
        
        _logger.LogInformation("Truck {TruckId} assigned to order {OrderId}", truckId, orderId);

        return true;
    }
}
