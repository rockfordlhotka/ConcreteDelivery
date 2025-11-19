# Messaging Constants

This directory contains centralized constants used throughout the ConcreteDelivery system for consistency and type safety.

## Files

### TruckStatus.cs
Defines all valid truck status values used across the system:
- `Available` - Truck is ready for assignment
- `Loading` - Truck is being loaded with materials
- `EnRoute` - Truck is traveling to the job site
- `AtJobSite` - Truck has arrived at the job site
- `Delivering` - Truck is actively pouring concrete
- `Returning` - Truck is returning to the plant
- `Washing` - Truck is being washed

**Usage:**
```csharp
using ConcreteDelivery.Messaging.Constants;

// Instead of:
if (truck.Status == "Available")

// Use:
if (truck.Status == TruckStatus.Available)
```

### RoutingKeys.cs
Defines all RabbitMQ routing keys for message routing. Organized by domain:
- `RoutingKeys.Truck.*` - Truck-related events
- `RoutingKeys.Order.*` - Order-related events

**Usage:**
```csharp
using ConcreteDelivery.Messaging.Constants;

await _messagePublisher.PublishAsync(
    message,
    exchange: ExchangeNames.TruckEvents,
    routingKey: RoutingKeys.Truck.StatusChanged);
```

### ExchangeNames.cs
Defines all RabbitMQ exchange names:
- `TruckEvents` - Exchange for truck-related events
- `OrderEvents` - Exchange for order-related events
- `TruckCommands` - Exchange for truck commands
- `OrderCommands` - Exchange for order commands

## Benefits

1. **Type Safety** - Compile-time checking prevents typos
2. **IntelliSense Support** - IDE autocomplete for all values
3. **Single Source of Truth** - All services use the same values
4. **Easier Refactoring** - Change values in one place
5. **Documentation** - Constants are self-documenting with XML comments
6. **Prevents Bugs** - Mismatched strings between services are eliminated

## Guidelines

- **Always** use these constants instead of hardcoded strings
- **Never** create duplicate status or routing key definitions in other projects
- Update the `TruckStatus.All` array when adding new statuses
- Add XML documentation comments for new constants
