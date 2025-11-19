namespace ConcreteDelivery.Messaging.Constants;

/// <summary>
/// Centralized exchange names for RabbitMQ
/// </summary>
public static class ExchangeNames
{
    public const string TruckEvents = "truck-events";
    public const string OrderEvents = "order-events";
    public const string TruckCommands = "truck-commands";
    public const string OrderCommands = "order-commands";
}
