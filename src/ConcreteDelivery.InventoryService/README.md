# Inventory Service

A background worker service that manages plant inventory levels by listening to truck events via RabbitMQ.

## Overview

This service is part of the Concrete Delivery system and is responsible for:

- Listening for `TruckStatusChangedEvent` messages from RabbitMQ
- Deducting materials from plant inventory when trucks enter "Loading" status
- Tracking inventory levels across multiple plants
- Providing OpenTelemetry instrumentation for observability

## Features

- **Event-Driven Architecture**: Subscribes to truck events via RabbitMQ
- **Automatic Inventory Management**: Deducts 10 units of each material (sand, gravel, concrete) per truck load
- **Plant-Based Tracking**: Gets plant assignment from the truck's current order
- **OpenTelemetry Integration**: Full observability with traces, metrics, and structured logging
- **PostgreSQL Persistence**: Stores inventory data in the shared database
- **Kubernetes-Ready**: Designed to run as a stateless worker in containers

## Material Deduction Logic

When a truck status changes to "Loading":

1. Service receives `TruckStatusChangedEvent` from RabbitMQ
2. Looks up the truck's assigned order to find the plant ID
3. Retrieves current inventory for that plant
4. Deducts 10 units of each material:
   - Sand: -10 units
   - Gravel: -10 units
   - Concrete: -10 units
5. Updates inventory timestamp
6. Logs the operation with before/after quantities

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=concretedelivery;Username=postgres;Password=postgres"
  },
  "RabbitMq": {
    "Server": "localhost",
    "User": "guest",
    "Password": "guest"
  }
}
```

### Environment Variables (Kubernetes)

The service reads configuration from environment variables in Kubernetes:

- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `RabbitMq__Server` - RabbitMQ host
- `RabbitMq__User` - RabbitMQ username
- `RabbitMq__Password` - RabbitMQ password
- `DOTNET_ENVIRONMENT` - Environment (Development/Production)

## Running Locally

```bash
# Restore dependencies
dotnet restore

# Run the service
dotnet run

# Or with specific configuration
DOTNET_ENVIRONMENT=Development dotnet run
```

## Building Docker Image

```bash
# From repository root
docker build -f src/ConcreteDelivery.InventoryService/Dockerfile -t rockylhotka/concretedelivery-inventory:latest .

# Push to registry
docker push rockylhotka/concretedelivery-inventory:latest
```

## Deploying to Kubernetes

```bash
# Apply the deployment
kubectl apply -f k8s/inventory-deployment.yaml

# Check status
kubectl get pods -n concretedelivery -l app=concretedelivery-inventory

# View logs
kubectl logs -n concretedelivery -l app=concretedelivery-inventory -f
```

## Observability

### OpenTelemetry Instrumentation

The service exports:

- **Traces**: Activity tracking for inventory operations
- **Metrics**: Counter for inventory deductions
- **Logs**: Structured logging with context

### Log Aggregation

For production deployments, consider adding a log aggregation solution:

**Recommended: Loki + Grafana**

```bash
# Install Loki stack
helm repo add grafana https://grafana.github.io/helm-charts
helm install loki grafana/loki-stack --set grafana.enabled=true -n monitoring
```

**Alternative: ELK Stack or OpenTelemetry Collector**

## Troubleshooting

### Service Not Processing Events

Check that:
1. RabbitMQ is accessible and credentials are correct
2. The exchange/queue exists (created by messaging infrastructure)
3. Other services are publishing `TruckStatusChangedEvent` messages

```bash
kubectl logs -n concretedelivery -l app=concretedelivery-inventory
```

### Inventory Not Deducting

Verify:
1. Truck has an assigned order (`CurrentOrderId` is not null)
2. Order has an assigned plant (`PlantId` is not null)
3. Plant has sufficient inventory (>= 10 units of each material)

### Database Connection Issues

Check the database secret:
```bash
kubectl get secret concretedelivery-db-secret -n concretedelivery -o yaml
```

## Architecture Notes

- **Stateless**: Can scale horizontally with multiple replicas
- **No HTTP Endpoints**: Pure background worker, no web API
- **Message-Driven**: Only acts on incoming events
- **Idempotent**: Safe to process same event multiple times (though messaging should prevent this)

## Dependencies

- **ConcreteDelivery.Data**: Entity Framework models and DbContext
- **ConcreteDelivery.Messaging**: RabbitMQ integration and message contracts
- **PostgreSQL**: Persistent storage
- **RabbitMQ**: Message broker

## Future Enhancements

- Add support for material replenishment events
- Implement low inventory alerts/notifications
- Add metrics dashboard for inventory levels
- Support for different material quantities per truck/order type
- Inventory reservation system for pending orders
