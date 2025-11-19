using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ConcreteDelivery.Messaging.Configuration;

namespace ConcreteDelivery.Messaging;

/// <summary>
/// Manages the connection to RabbitMQ
/// </summary>
public class RabbitMqConnection : IRabbitMqConnection
{
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private readonly object _lock = new();
    private bool _disposed;

    public RabbitMqConnection(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConnected => _connection?.IsOpen ?? false;

    public IConnection GetConnection()
    {
        if (IsConnected)
        {
            return _connection!;
        }

        lock (_lock)
        {
            if (IsConnected)
            {
                return _connection!;
            }

            _logger.LogInformation("Creating RabbitMQ connection to {Server}:{Port}", _options.Server, _options.Port);

            var factory = new ConnectionFactory
            {
                HostName = _options.Server,
                Port = _options.Port,
                UserName = _options.User,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();

            _logger.LogInformation("RabbitMQ connection established");

            return _connection;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _connection?.Dispose();
            _logger.LogInformation("RabbitMQ connection disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ connection");
        }
    }
}
