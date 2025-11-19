namespace ConcreteDelivery.Messaging.Constants;

/// <summary>
/// Centralized routing keys for RabbitMQ message routing
/// </summary>
public static class RoutingKeys
{
    /// <summary>
    /// Truck-related routing keys
    /// </summary>
    public static class Truck
    {
        public const string StatusChanged = "truck.status.changed";
        public const string MaterialsLoaded = "truck.materials.loaded";
        public const string ArrivedAtJobSite = "truck.arrived.jobsite";
        public const string PouringStarted = "truck.pouring.started";
        public const string PouringCompleted = "truck.pouring.completed";
        public const string ReturnedToPlant = "truck.returned.plant";
        public const string WashStarted = "truck.wash.started";
        public const string WashCompleted = "truck.wash.completed";
    }
    
    /// <summary>
    /// Order-related routing keys
    /// </summary>
    public static class Order
    {
        public const string Created = "order.created";
        public const string Updated = "order.updated";
        public const string Cancelled = "order.cancelled";
        public const string StatusChanged = "order.status.changed";
        public const string InTransit = "order.status.intransit";
        public const string Delivered = "order.delivered";
        public const string TruckAssigned = "order.truck.assigned";
    }
}
