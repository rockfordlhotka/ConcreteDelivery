using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.Messaging;

/// <summary>
/// Publishes messages to RabbitMQ
/// </summary>
public class MessagePublisher : IMessagePublisher
{
    private readonly IRabbitMqConnection _connection;
    private readonly ILogger<MessagePublisher> _logger;
    private const string CommandExchange = "concrete.commands";
    private const string EventExchange = "concrete.events";

    public MessagePublisher(
        IRabbitMqConnection connection,
        ILogger<MessagePublisher> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<TMessage>(TMessage message, string exchange, string routingKey = "") 
        where TMessage : IMessage
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(exchange)) throw new ArgumentException("Exchange name is required", nameof(exchange));

        var connection = _connection.GetConnection();
        using var channel = await connection.CreateChannelAsync();

        // Declare the exchange (idempotent)
        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(message, message.GetType(), jsonOptions);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.MessageId.ToString(),
            Timestamp = new AmqpTimestamp(new DateTimeOffset(message.CreatedAt).ToUnixTimeSeconds()),
            Type = message.GetType().Name
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Published message {MessageType} with ID {MessageId} to exchange {Exchange} with routing key {RoutingKey}",
            message.GetType().Name, message.MessageId, exchange, routingKey);
    }

    public Task PublishCommandAsync<TCommand>(TCommand command) 
        where TCommand : TruckCommand
    {
        var routingKey = $"truck.{command.TruckId}.{command.GetType().Name.Replace("Command", "").ToLowerInvariant()}";
        return PublishAsync(command, CommandExchange, routingKey);
    }

    public Task PublishEventAsync<TEvent>(TEvent eventMessage) 
        where TEvent : TruckEvent
    {
        var routingKey = $"truck.{eventMessage.TruckId}.{eventMessage.GetType().Name.Replace("Event", "").ToLowerInvariant()}";
        return PublishAsync(eventMessage, EventExchange, routingKey);
    }
}
