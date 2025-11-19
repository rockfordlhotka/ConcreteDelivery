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

    /// <summary>
    /// Publishes a command message
    /// </summary>
    Task PublishCommandAsync<TCommand>(TCommand command) 
        where TCommand : TruckCommand;

    /// <summary>
    /// Publishes an event message
    /// </summary>
    Task PublishEventAsync<TEvent>(TEvent eventMessage) 
        where TEvent : TruckEvent;
}
