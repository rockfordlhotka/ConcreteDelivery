namespace ConcreteDelivery.Messaging.Messages;

/// <summary>
/// Base class for all truck events
/// </summary>
public abstract record TruckEvent : IMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required string TruckId { get; init; }
}

/// <summary>
/// Event when truck status changes
/// </summary>
public record TruckStatusChangedEvent : TruckEvent
{
    public required string PreviousStatus { get; init; }
    public required string NewStatus { get; init; }
}

/// <summary>
/// Event when materials are loaded
/// </summary>
public record MaterialsLoadedEvent : TruckEvent
{
    public required Dictionary<string, decimal> Materials { get; init; }
}

/// <summary>
/// Event when truck departs for job site
/// </summary>
public record DepartedForJobSiteEvent : TruckEvent
{
    public required string JobSiteId { get; init; }
    public required string Address { get; init; }
}

/// <summary>
/// Event when truck arrives at job site
/// </summary>
public record ArrivedAtJobSiteEvent : TruckEvent
{
    public required string JobSiteId { get; init; }
}

/// <summary>
/// Event when pouring starts
/// </summary>
public record PouringStartedEvent : TruckEvent
{
    public required string JobSiteId { get; init; }
}

/// <summary>
/// Event when pouring completes
/// </summary>
public record PouringCompletedEvent : TruckEvent
{
    public required string JobSiteId { get; init; }
    public required decimal AmountPoured { get; init; }
}

/// <summary>
/// Event when wash starts
/// </summary>
public record WashStartedEvent : TruckEvent
{
}

/// <summary>
/// Event when wash completes
/// </summary>
public record WashCompletedEvent : TruckEvent
{
}

/// <summary>
/// Event when truck returns to plant
/// </summary>
public record ReturnedToPlantEvent : TruckEvent
{
}

/// <summary>
/// Event when truck becomes idle
/// </summary>
public record TruckIdleEvent : TruckEvent
{
}
