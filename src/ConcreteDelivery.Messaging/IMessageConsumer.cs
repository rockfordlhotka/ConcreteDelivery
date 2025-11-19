using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.Messaging;

/// <summary>
/// Interface for consuming messages from RabbitMQ
/// </summary>
public interface IMessageConsumer
{
    /// <summary>
    /// Starts consuming messages from a queue
    /// </summary>
    Task StartConsumingAsync<TMessage>(
        string queueName, 
        Func<TMessage, Task> handler,
        string? exchangeName = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default) 
        where TMessage : IMessage;

    /// <summary>
    /// Stops consuming messages
    /// </summary>
    Task StopConsumingAsync();
}
