# ConcreteDelivery.Messaging

A .NET library for RabbitMQ messaging support in the Concrete Delivery system.

## Features

- **Message Contracts**: Strongly-typed messages for truck commands and events
- **Publisher**: Publish commands and events to RabbitMQ exchanges
- **Consumer**: Consume messages from RabbitMQ queues with automatic acknowledgment
- **Connection Management**: Automatic connection recovery and resilience
- **Dependency Injection**: Easy integration with .NET DI container

## Message Types

### Commands (TruckCommand)
- `LoadMaterialsCommand`: Start loading materials at the plant
- `EnrouteToJobSiteCommand`: Truck is enroute to job site
- `WaitingAtJobSiteCommand`: Truck is waiting at job site
- `StartPouringCommand`: Start pouring concrete
- `CompletePouringCommand`: Complete pouring concrete
- `WashTruckCommand`: Start washing the truck
- `CompleteWashCommand`: Complete washing the truck
- `ReturnToPlantCommand`: Return to plant
- `IdleAtPlantCommand`: Truck is idle at plant

### Events (TruckEvent)
- `TruckStatusChangedEvent`: Truck status has changed
- `MaterialsLoadedEvent`: Materials have been loaded
- `DepartedForJobSiteEvent`: Truck departed for job site
- `ArrivedAtJobSiteEvent`: Truck arrived at job site
- `PouringStartedEvent`: Pouring has started
- `PouringCompletedEvent`: Pouring has completed
- `WashStartedEvent`: Wash has started
- `WashCompletedEvent`: Wash has completed
- `ReturnedToPlantEvent`: Truck returned to plant
- `TruckIdleEvent`: Truck is now idle

## Configuration

The library uses .NET user secrets for RabbitMQ configuration. The credentials are stored securely and never committed to source control.

### Required Configuration

```json
{
  "RabbitMq": {
    "Server": "default-rabbitmq-amqp.tail920062.ts.net",
    "User": "concretedelivery",
    "Password": "***",
    "Port": 5672,
    "VirtualHost": "/"
  }
}
```

### Setting User Secrets

```bash
cd src/ConcreteDelivery.Web
dotnet user-secrets set "RabbitMq:Server" "default-rabbitmq-amqp.tail920062.ts.net"
dotnet user-secrets set "RabbitMq:User" "concretedelivery"
dotnet user-secrets set "RabbitMq:Password" "your-password"
```

## Usage

### Register Services

In `Program.cs`:

```csharp
using ConcreteDelivery.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add RabbitMQ messaging services
builder.Services.AddRabbitMqMessaging(builder.Configuration);

var app = builder.Build();
```

### Publishing Messages

```csharp
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Messages;

public class TruckController
{
    private readonly IMessagePublisher _publisher;

    public TruckController(IMessagePublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task StartPouring(string truckId, string jobSiteId)
    {
        var command = new StartPouringCommand
        {
            TruckId = truckId,
            JobSiteId = jobSiteId
        };

        await _publisher.PublishCommandAsync(command);
    }

    public async Task NotifyStatusChanged(string truckId, string oldStatus, string newStatus)
    {
        var evt = new TruckStatusChangedEvent
        {
            TruckId = truckId,
            PreviousStatus = oldStatus,
            NewStatus = newStatus
        };

        await _publisher.PublishEventAsync(evt);
    }
}
```

### Consuming Messages

```csharp
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Messages;
using Microsoft.Extensions.Hosting;

public class TruckCommandHandler : BackgroundService
{
    private readonly IMessageConsumer _consumer;
    private readonly ILogger<TruckCommandHandler> _logger;

    public TruckCommandHandler(
        IMessageConsumer consumer,
        ILogger<TruckCommandHandler> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _consumer.StartConsumingAsync<StartPouringCommand>(
            queueName: "truck.commands.startpouring",
            handler: HandleStartPouring,
            exchangeName: "concrete.commands",
            routingKey: "truck.*.startpouring",
            cancellationToken: stoppingToken);
    }

    private async Task HandleStartPouring(StartPouringCommand command)
    {
        _logger.LogInformation(
            "Processing StartPouringCommand for truck {TruckId} at job site {JobSiteId}",
            command.TruckId, command.JobSiteId);

        // Process the command...
        await Task.CompletedTask;
    }
}
```

## Architecture

### Exchanges

- `concrete.commands`: Topic exchange for truck commands
- `concrete.events`: Topic exchange for truck events

### Routing Keys

Commands and events use the following routing key pattern:
- Commands: `truck.{truckId}.{commandname}`
- Events: `truck.{truckId}.{eventname}`

Examples:
- `truck.truck-001.startpouring`
- `truck.truck-002.materialsloaded`

### Message Format

Messages are serialized as JSON with the following properties:
- `messageId`: Unique identifier (GUID)
- `createdAt`: Timestamp when message was created
- `truckId`: The truck identifier
- Additional properties specific to the message type

## Kubernetes Deployment

For Kubernetes deployments, use ConfigMaps or Secrets to inject RabbitMQ configuration:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: rabbitmq-credentials
type: Opaque
stringData:
  server: default-rabbitmq-amqp.tail920062.ts.net
  user: concretedelivery
  password: your-password
```

Then mount as environment variables in your deployment.
