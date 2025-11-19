namespace ConcreteDelivery.Messaging.Messages;

/// <summary>
/// Base class for all order events
/// </summary>
public abstract record OrderEvent : IMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required int OrderId { get; init; }
}

/// <summary>
/// Event when a new order is created
/// </summary>
public record OrderCreatedEvent : OrderEvent
{
    public required string CustomerName { get; init; }
    public required int DistanceMiles { get; init; }
    public int? PlantId { get; init; }
    public required string Status { get; init; }
}

/// <summary>
/// Event when an order is updated
/// </summary>
public record OrderUpdatedEvent : OrderEvent
{
    public required string CustomerName { get; init; }
    public required int DistanceMiles { get; init; }
    public int? PlantId { get; init; }
    public int? TruckId { get; init; }
    public required string Status { get; init; }
}

/// <summary>
/// Event when an order is cancelled
/// </summary>
public record OrderCancelledEvent : OrderEvent
{
    public required string Reason { get; init; }
    public required string CustomerName { get; init; }
}

/// <summary>
/// Event when a truck is assigned to an order
/// </summary>
public record TruckAssignedToOrderEvent : OrderEvent
{
    public required int TruckId { get; init; }
    public required string TruckDriverName { get; init; }
}

/// <summary>
/// Event when a plant is assigned to an order
/// </summary>
public record PlantAssignedToOrderEvent : OrderEvent
{
    public required int PlantId { get; init; }
    public required string PlantName { get; init; }
}

/// <summary>
/// Event when order status changes
/// </summary>
public record OrderStatusChangedEvent : OrderEvent
{
    public required string PreviousStatus { get; init; }
    public required string NewStatus { get; init; }
}

/// <summary>
/// Event when order is marked as in transit
/// </summary>
public record OrderInTransitEvent : OrderEvent
{
    public required int TruckId { get; init; }
}

/// <summary>
/// Event when order is delivered
/// </summary>
public record OrderDeliveredEvent : OrderEvent
{
    public required int TruckId { get; init; }
    public required DateTime DeliveredAt { get; init; }
}
