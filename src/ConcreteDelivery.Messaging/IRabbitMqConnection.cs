using RabbitMQ.Client;

namespace ConcreteDelivery.Messaging;

/// <summary>
/// Interface for managing RabbitMQ connection
/// </summary>
public interface IRabbitMqConnection : IDisposable
{
    /// <summary>
    /// Gets a connection to RabbitMQ
    /// </summary>
    IConnection GetConnection();

    /// <summary>
    /// Checks if the connection is open
    /// </summary>
    bool IsConnected { get; }
}
