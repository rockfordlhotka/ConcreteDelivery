using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ConcreteDelivery.Messaging.Messages;

namespace ConcreteDelivery.Messaging;

/// <summary>
/// Consumes messages from RabbitMQ
/// </summary>
public class MessageConsumer : IMessageConsumer, IDisposable
{
    private readonly IRabbitMqConnection _connection;
    private readonly ILogger<MessageConsumer> _logger;
    private IChannel? _channel;
    private string? _consumerTag;
    private bool _disposed;

    public MessageConsumer(
        IRabbitMqConnection connection,
        ILogger<MessageConsumer> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartConsumingAsync<TMessage>(
        string queueName,
        Func<TMessage, Task> handler,
        string? exchangeName = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        if (string.IsNullOrWhiteSpace(queueName)) 
            throw new ArgumentException("Queue name is required", nameof(queueName));
        if (handler == null) 
            throw new ArgumentNullException(nameof(handler));

        var connection = _connection.GetConnection();
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Set prefetch count for fair dispatch
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken);

        // Declare the queue (idempotent)
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        // If exchange and routing key provided, bind the queue
        if (!string.IsNullOrWhiteSpace(exchangeName))
        {
            await _channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: routingKey ?? "#",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Bound queue {QueueName} to exchange {ExchangeName} with routing key {RoutingKey}",
                queueName, exchangeName, routingKey ?? "#");
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        
        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            try
            {
                var body = eventArgs.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                _logger.LogDebug("Received message from queue {QueueName}: {Json}", queueName, json);

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var message = JsonSerializer.Deserialize<TMessage>(json, jsonOptions);

                if (message != null)
                {
                    await handler(message);

                    // Acknowledge the message
                    await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);

                    _logger.LogInformation(
                        "Successfully processed message {MessageId} from queue {QueueName}",
                        message.MessageId, queueName);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize message from queue {QueueName}", queueName);
                    // Negative acknowledge - requeue the message
                    await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                // Negative acknowledge - requeue the message
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Started consuming from queue {QueueName} with consumer tag {ConsumerTag}", 
            queueName, _consumerTag);
    }

    public async Task StopConsumingAsync()
    {
        if (_channel != null && !string.IsNullOrWhiteSpace(_consumerTag))
        {
            await _channel.BasicCancelAsync(_consumerTag);
            _logger.LogInformation("Stopped consuming with consumer tag {ConsumerTag}", _consumerTag);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _channel?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing message consumer channel");
        }
    }
}
