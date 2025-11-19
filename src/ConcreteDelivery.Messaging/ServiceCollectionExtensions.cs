using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ConcreteDelivery.Messaging.Configuration;

namespace ConcreteDelivery.Messaging;

/// <summary>
/// Extension methods for configuring RabbitMQ messaging services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ messaging services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<RabbitMqOptions>(options =>
            configuration.GetSection(RabbitMqOptions.SectionName).Bind(options));

        // Register services
        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, MessagePublisher>();
        services.AddTransient<IMessageConsumer, MessageConsumer>();

        return services;
    }

    /// <summary>
    /// Adds RabbitMQ messaging services with custom configuration action
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        Action<RabbitMqOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Register services
        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, MessagePublisher>();
        services.AddTransient<IMessageConsumer, MessageConsumer>();

        return services;
    }
}
