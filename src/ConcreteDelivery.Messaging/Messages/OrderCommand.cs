namespace ConcreteDelivery.Messaging.Messages;

/// <summary>
/// Base class for all order commands
/// </summary>
public abstract record OrderCommand : IMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required int OrderId { get; init; }
}

/// <summary>
/// Command to create a new order
/// </summary>
public record CreateOrderCommand : IMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required string CustomerName { get; init; }
    public required int DistanceMiles { get; init; }
    public int? PlantId { get; init; }
}

/// <summary>
/// Command to update an existing order
/// </summary>
public record UpdateOrderCommand : OrderCommand
{
    public required string CustomerName { get; init; }
    public required int DistanceMiles { get; init; }
    public int? PlantId { get; init; }
    public int? TruckId { get; init; }
}

/// <summary>
/// Command to cancel an order
/// </summary>
public record CancelOrderCommand : OrderCommand
{
    public required string Reason { get; init; }
}

/// <summary>
/// Command to assign a truck to an order
/// </summary>
public record AssignTruckToOrderCommand : OrderCommand
{
    public required int TruckId { get; init; }
}

/// <summary>
/// Command to assign a plant to an order
/// </summary>
public record AssignPlantToOrderCommand : OrderCommand
{
    public required int PlantId { get; init; }
}

/// <summary>
/// Command to mark order as in transit
/// </summary>
public record OrderInTransitCommand : OrderCommand
{
}

/// <summary>
/// Command to mark order as delivered
/// </summary>
public record OrderDeliveredCommand : OrderCommand
{
}
