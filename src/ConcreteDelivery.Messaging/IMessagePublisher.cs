using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.Messaging;

/// <summary>
/// Interface for publishing messages to RabbitMQ
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the specified exchange
    /// </summary>
    Task PublishAsync<TMessage>(TMessage message, string exchange, string routingKey = "") 
        where TMessage : IMessage;

}
