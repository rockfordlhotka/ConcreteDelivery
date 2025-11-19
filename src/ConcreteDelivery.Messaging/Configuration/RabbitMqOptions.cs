namespace ConcreteDelivery.Messaging.Configuration;

/// <summary>
/// Configuration options for RabbitMQ connection
/// </summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// RabbitMQ server hostname
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// Username for authentication
    /// </summary>
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Password for authentication
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Port number (default 5672 for AMQP)
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Virtual host (default "/")
    /// </summary>
    public string VirtualHost { get; set; } = "/";
}
