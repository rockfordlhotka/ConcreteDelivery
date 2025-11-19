# Concrete Delivery - Truck Status Service

A background service that simulates truck operations with compressed time for demo purposes. This service listens for truck assignments and simulates the entire workflow from loading to delivery and back.

## Overview

The Truck Status Service is responsible for:

- Listening for `TruckAssignedToOrderEvent` messages from the Job Workflow Service
- Simulating the complete truck workflow with realistic timing
- Publishing status updates and events at each phase
- Updating truck and order status in the PostgreSQL database

## Simulation Timing

The service uses compressed time to make demonstrations engaging. Total time for an average delivery is approximately 80 seconds:

| Phase | Time | Description |
|-------|------|-------------|
| **Loading** | 15 seconds | Loading materials at the plant |
| **Travel to Site** | 2 seconds per mile | Driving from plant to job site |
| **Delivery/Pouring** | 15 seconds | Pouring concrete at the job site |
| **Travel to Yard** | 2 seconds per mile | Returning from job site to yard |
| **Washing** | 10 seconds | Washing the truck at the yard |

For example:
- **5-mile delivery**: 15 + 10 + 15 + 10 + 10 = **60 seconds**
- **10-mile delivery**: 15 + 20 + 15 + 20 + 10 = **80 seconds**
- **15-mile delivery**: 15 + 30 + 15 + 30 + 10 = **100 seconds**

## Workflow Phases

### 1. Loading Phase
- Updates truck status to "Loading"
- Updates order status to "Loading"
- Publishes `TruckStatusChangedEvent` and `MaterialsLoadedEvent`

### 2. Travel to Job Site
- Updates truck status to "EnRoute"
- Updates order status to "InTransit"
- Publishes `TruckStatusChangedEvent`, `OrderInTransitEvent`, and `ArrivedAtJobSiteEvent`

### 3. Delivery/Pouring
- Updates truck status to "Delivering"
- Updates order status to "Delivering"
- Publishes `PouringStartedEvent` and `PouringCompletedEvent`

### 4. Travel to Yard
- Updates truck status to "Returning"
- Publishes `TruckStatusChangedEvent` and `ReturnedToPlantEvent`

### 5. Washing
- Updates truck status to "Washing"
- Publishes `WashStartedEvent` and `WashCompletedEvent`

### 6. Completion
- Updates truck status to "Available" (clears current order)
- Updates order status to "Delivered"
- Publishes `TruckIdleEvent` and `OrderDeliveredEvent`

## Configuration

The service requires configuration for:

### Database Connection
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres-host;Port=5432;Database=concretedelivery;Username=postgres;Password=your-password"
  }
}
```

### RabbitMQ Connection
```json
{
  "RabbitMq": {
    "HostName": "rabbitmq-host",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

## Message Integration

### Consumes
- **Exchange**: `order-events`
- **Routing Key**: `order.truck.assigned`
- **Message**: `TruckAssignedToOrderEvent`

### Publishes
The service publishes to two exchanges:

#### Truck Events (`truck-events` exchange)
- `truck.status.changed` - `TruckStatusChangedEvent`
- `truck.materials.loaded` - `MaterialsLoadedEvent`
- `truck.arrived.jobsite` - `ArrivedAtJobSiteEvent`
- `truck.pouring.started` - `PouringStartedEvent`
- `truck.pouring.completed` - `PouringCompletedEvent`
- `truck.returned.plant` - `ReturnedToPlantEvent`
- `truck.wash.started` - `WashStartedEvent`
- `truck.wash.completed` - `WashCompletedEvent`
- `truck.idle` - `TruckIdleEvent`

#### Order Events (`order-events` exchange)
- `order.status.intransit` - `OrderInTransitEvent`
- `order.delivered` - `OrderDeliveredEvent`

## Building

### Local Build
```bash
dotnet build src/ConcreteDelivery.TruckStatusService/ConcreteDelivery.TruckStatusService.csproj
```

### Docker Build
```bash
# From repository root
docker build -f src/ConcreteDelivery.TruckStatusService/Dockerfile -t rockylhotka/concretedelivery-truckstatus:latest .
```

Or use the build script:
```bash
./build-truckstatus.sh
```

## Running Locally

```bash
cd src/ConcreteDelivery.TruckStatusService
dotnet run
```

Make sure PostgreSQL and RabbitMQ are accessible before starting the service.

## Deployment

### Kubernetes Deployment

The service is deployed as a Kubernetes Deployment without an associated Service, as it only communicates internally via the database and RabbitMQ.

```bash
kubectl apply -f k8s/truckstatus-deployment.yaml
```

The deployment:
- Runs as a single replica
- Uses secrets for database and RabbitMQ credentials
- Has health checks based on process running
- Includes resource requests and limits

### Environment Variables

Set these environment variables in Kubernetes:

- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `RabbitMq__Server` - RabbitMQ hostname
- `RabbitMq__User` - RabbitMQ username
- `RabbitMq__Password` - RabbitMQ password
- `DOTNET_ENVIRONMENT` - Environment (Production, Development)

## Dependencies

- **ConcreteDelivery.Data** - EF Core entities and DbContext
- **ConcreteDelivery.Messaging** - RabbitMQ messaging infrastructure
- **PostgreSQL** - Database for persisting truck and order status
- **RabbitMQ** - Message broker for event-driven communication

## Observability

The service includes:
- **OpenTelemetry** tracing with ActivitySource
- **Structured logging** with console output
- **Metrics** for monitoring (via OpenTelemetry)

All major operations are traced and logged for debugging and monitoring purposes.

## Architecture

This is a background worker service (no HTTP endpoints) that:
- Uses a hosted service pattern
- Maintains concurrent truck simulations in separate tasks
- Each truck gets its own cancellation token for graceful shutdown
- Uses scoped services for database access within each simulation

## Notes

- The service can handle multiple trucks simultaneously
- Each truck simulation runs independently in its own background task
- Travel time is calculated based on the order's `distance_miles` field
- The simulation automatically handles cancellation on service shutdown
- All status changes are persisted to the database and published as events
