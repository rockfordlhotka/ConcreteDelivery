namespace ConcreteDelivery.Messaging.Constants;

/// <summary>
/// Centralized truck status values used throughout the system
/// </summary>
public static class TruckStatus
{
    /// <summary>
    /// Truck is available and ready for assignment
    /// </summary>
    public const string Available = "Available";
    
    /// <summary>
    /// Truck is assigned to an order but not yet loading
    /// </summary>
    public const string Assigned = "Assigned";

    /// <summary>
    /// Truck is being loaded with materials
    /// </summary>
    public const string Loading = "Loading";
    
    /// <summary>
    /// Truck is traveling to the job site
    /// </summary>
    public const string EnRoute = "EnRoute";
    
    /// <summary>
    /// Truck has arrived at the job site
    /// </summary>
    public const string AtJobSite = "AtJobSite";
    
    /// <summary>
    /// Truck is actively delivering/pouring concrete
    /// </summary>
    public const string Delivering = "Delivering";
    
    /// <summary>
    /// Truck is returning to the plant
    /// </summary>
    public const string Returning = "Returning";
    
    /// <summary>
    /// Truck is being washed
    /// </summary>
    public const string Washing = "Washing";
    
    /// <summary>
    /// Get all valid truck statuses
    /// </summary>
    public static readonly string[] All = 
    {
        Available,
        Assigned,
        Loading,
        EnRoute,
        AtJobSite,
        Delivering,
        Returning,
        Washing
    };
}
