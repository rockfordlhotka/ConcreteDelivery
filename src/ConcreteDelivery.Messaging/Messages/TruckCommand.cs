namespace ConcreteDelivery.Messaging.Messages;

/// <summary>
/// Base class for all truck commands
/// </summary>
public abstract record TruckCommand : IMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required string TruckId { get; init; }
}

/// <summary>
/// Command to start loading materials at the plant
/// </summary>
public record LoadMaterialsCommand : TruckCommand
{
    public required Dictionary<string, decimal> Materials { get; init; }
}

/// <summary>
/// Command to mark truck as enroute to job site
/// </summary>
public record EnrouteToJobSiteCommand : TruckCommand
{
    public required string JobSiteId { get; init; }
    public required string Address { get; init; }
}

/// <summary>
/// Command to mark truck as waiting at job site
/// </summary>
public record WaitingAtJobSiteCommand : TruckCommand
{
    public required string JobSiteId { get; init; }
}

/// <summary>
/// Command to start pouring concrete
/// </summary>
public record StartPouringCommand : TruckCommand
{
    public required string JobSiteId { get; init; }
}

/// <summary>
/// Command to complete pouring concrete
/// </summary>
public record CompletePouringCommand : TruckCommand
{
    public required string JobSiteId { get; init; }
    public required decimal AmountPoured { get; init; }
}

/// <summary>
/// Command to start washing the truck
/// </summary>
public record WashTruckCommand : TruckCommand
{
}

/// <summary>
/// Command to complete washing the truck
/// </summary>
public record CompleteWashCommand : TruckCommand
{
}

/// <summary>
/// Command to return to plant
/// </summary>
public record ReturnToPlantCommand : TruckCommand
{
}

/// <summary>
/// Command to mark truck as idle at plant
/// </summary>
public record IdleAtPlantCommand : TruckCommand
{
}
