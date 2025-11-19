# Concrete Delivery - Job Workflow Service

A background orchestration service that manages the complete order fulfillment workflow. This service watches for new orders, assigns available trucks, handles order cancellations, and tracks order completion.

## Overview

The Job Workflow Service is responsible for:

- Listening for new order creation events
- Finding available trucks and assigning them to orders
- Handling order cancellations and returning trucks to the plant
- Marking orders as complete when delivery is finished
- Maintaining order and truck status in the PostgreSQL database

## Architecture

This service implements three distinct background workers:

### 1. Job Workflow Orchestrator
**Purpose**: Assign available trucks to new orders

**Workflow**:
1. Listens for `OrderCreatedEvent` messages
2. Queries database for an available truck (status = "Available")
3. Updates order status to "Assigned" and assigns the truck
4. Updates truck status to "Assigned" and links to the order
5. Publishes `TruckAssignedToOrderEvent` to start the truck workflow

**Key Features**:
- Automatically finds the first available truck
- Handles scenarios where no trucks are available (order remains "Pending")
- Thread-safe database operations
- Full tracing and logging

### 2. Order Cancellation Handler
**Purpose**: Handle cancelled orders and return trucks

**Workflow**:
1. Listens for `OrderCancelledEvent` messages
2. Retrieves the truck assigned to the cancelled order
3. Updates truck status to "Returning"
4. Publishes `ReturnToPlantCommand` to send truck back to plant
5. Publishes `TruckStatusChangedEvent` for status tracking

**Key Features**:
- Safely handles cancellations when no truck is assigned
- Immediately redirects assigned trucks back to plant
- Clears the order association from the truck

### 3. Order Completion Handler
**Purpose**: Mark orders as delivered when trucks complete their workflow

**Workflow**:
1. Listens for `OrderDeliveredEvent` messages
2. Updates order status to "Delivered" in the database
3. Logs completion for tracking and auditing

**Key Features**:
- Final step in the order lifecycle
- Ensures order status reflects completion
- Provides audit trail of deliveries

## Message Integration

### Consumes

| Exchange | Routing Key | Message Type | Handler |
|----------|-------------|--------------|---------|
| `order-events` | `order.created` | `OrderCreatedEvent` | JobWorkflowOrchestrator |
| `order-events` | `order.cancelled` | `OrderCancelledEvent` | OrderCancellationHandler |
| `order-events` | `order.delivered` | `OrderDeliveredEvent` | OrderCompletionHandler |

### Publishes

| Exchange | Routing Key | Message Type | Purpose |
|----------|-------------|--------------|---------|
| `order-events` | `order.truck.assigned` | `TruckAssignedToOrderEvent` | Trigger truck workflow |
| `truck-events` | `truck.status.changed` | `TruckStatusChangedEvent` | Track truck status changes |
| `truck-commands` | `truck.{truckId}.returntoplant` | `ReturnToPlantCommand` | Send truck back to plant |

## Order Lifecycle

```
1. Order Created (Pending)
   ↓
2. Truck Assigned (Assigned) ← Job Workflow Orchestrator
   ↓
3. Truck Workflow Begins
   ├─ Loading
   ├─ EnRoute (InTransit)
   ├─ Delivering
   ├─ Returning
   └─ Washing
   ↓
4. Order Delivered (Delivered) ← Order Completion Handler

Alternative Path:
Order Cancelled → Truck Returning ← Order Cancellation Handler
```

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
    "Server": "rabbitmq-host",
    "Port": 5672,
    "User": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

## Building

### Local Build
```bash
dotnet build src/ConcreteDelivery.JobWorkflowService/ConcreteDelivery.JobWorkflowService.csproj
```

### Docker Build
```bash
# From repository root
docker build -f src/ConcreteDelivery.JobWorkflowService/Dockerfile -t rockylhotka/concretedelivery-jobworkflow:latest .
```

Or use the build script:
```bash
./build-jobworkflow.sh
```

## Running Locally

```bash
cd src/ConcreteDelivery.JobWorkflowService
dotnet run
```

Make sure PostgreSQL and RabbitMQ are accessible before starting the service.

## Deployment

### Kubernetes Deployment

The service is deployed as a Kubernetes Deployment without an associated Service, as it only communicates internally via the database and RabbitMQ.

```bash
kubectl apply -f k8s/jobworkflow-deployment.yaml
```

The deployment:
- Runs as a single replica (can be scaled if needed)
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
- **PostgreSQL** - Database for persisting order and truck status
- **RabbitMQ** - Message broker for event-driven communication

## Observability

The service includes:
- **OpenTelemetry** tracing with ActivitySource
- **Structured logging** with console output
- **Metrics** for monitoring (via OpenTelemetry)

All major operations are traced and logged for debugging and monitoring purposes.

## Database Operations

The service uses `JobWorkflowRepository` for all database operations:

| Method | Purpose |
|--------|---------|
| `GetAvailableTruckAsync()` | Find first available truck |
| `AssignTruckToOrderAsync()` | Link truck to order |
| `GetOrderAsync()` | Retrieve order details |
| `UpdateOrderStatusAsync()` | Change order status |
| `UpdateTruckStatusAsync()` | Change truck status |
| `GetTruckIdForOrderAsync()` | Get truck assigned to order |

All operations use `IDbContextFactory` for proper scoping in background services.

## Scaling Considerations

The service is designed to be scalable:

- Multiple instances can run concurrently
- RabbitMQ distributes messages across instances
- Each instance processes messages independently
- Database operations are transactional and safe for concurrent access

**Recommendation**: Start with 1 replica and scale up if message processing lags behind order creation rate.

## Error Handling

The service includes robust error handling:

- Failed message processing is logged with full context
- Messages are requeued on failure (RabbitMQ behavior)
- Database transaction failures are logged and retried by RabbitMQ
- OpenTelemetry ActivityStatus tracks success/failure

## Testing

To test the service:

1. Start PostgreSQL and RabbitMQ
2. Run the service
3. Create an order (via Web UI or direct message)
4. Watch logs to see:
   - Order received
   - Available truck found
   - Truck assigned
   - Assignment event published

## Future Enhancements

Potential improvements:
- **Priority Queue**: Handle urgent orders first
- **Truck Pool Management**: Assign trucks from specific plants
- **Load Balancing**: Consider truck location when assigning
- **Retry Logic**: Implement retry for orders without available trucks
- **Notification Service**: Alert dispatchers when no trucks available
- **Metrics Dashboard**: Track assignment rates, wait times, etc.

## Notes

- The service operates entirely through message-driven events
- No HTTP endpoints (pure background worker)
- Each background service runs independently
- All three services start together in the same process
- Graceful shutdown ensures message processing completes
