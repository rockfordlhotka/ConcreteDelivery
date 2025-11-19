# Concrete Delivery System Demo on Kubernetes

## Overview

This demo showcases how Kubernetes is a great home for .NET applications by simulating a **concrete delivery system**.  

The system includes:

- A **Blazor front-end** for dispatchers to manage trucks and deliveries.
- Multiple **.NET microservices** communicating via HTTP and RabbitMQ.
- A **PostgreSQL database** for persistence.
- Kubernetes features such as scaling, failover, rolling updates, and service discovery.

---

## Scenario

Concrete trucks operate in a cycle:

1. **Load Materials** (sand, rock, cement, water) at the plant.
2. **Enroute** to job site.
3. **Waiting** at job site.
4. **Pouring Concrete**.
5. **Washing Truck**.
6. **Returning to Plant**.
7. **Idle** at plant.

The dispatcher dashboard (Blazor) tracks trucks through these states and issues commands.

---

## Architecture

### Front-End

- **Blazor Web App**
  - Displays truck status and delivery orders.
  - Sends commands to backend services.
  - Calls HTTP APIs for synchronous operations.

### Services

1. **Inventory Service (Background Worker)**
   - Listens for truck status changes via RabbitMQ.
   - Deducts materials from plant inventory when trucks start loading.
   - Uses PostgreSQL for inventory persistence.
   - Demonstrates event-driven architecture.

2. **Truck Status Service (Background Worker)**
   - Simulates truck operations with compressed time for demo purposes.
   - Listens for truck assignments and simulates complete workflow.
   - Updates truck and order status in PostgreSQL.
   - Publishes status events at each phase via RabbitMQ.
   - Total cycle time: ~60-100 seconds depending on distance.

3. **Job Workflow Service (Planned)**
   - Assigns trucks to orders based on availability.
   - Orchestrates the flow of work through the system.
   - Publishes truck assignment events.
   - Demonstrates async orchestration and resilience.

### Database

- **PostgreSQL**
  - Stores truck states, delivery history, and material inventory.

---

## Kubernetes Demo Highlights

- **Event-Driven Architecture**: Services communicate via RabbitMQ for loose coupling and resilience.
- **Background Workers**: Inventory and Truck Status services run as background workers without HTTP endpoints.
- **Async Workflow**: Watch orders progress through the system in real-time with compressed timing.
- **Persistence**: PostgreSQL ensures truck status, orders, and inventory are maintained.
- **Service Isolation**: Each service can be deployed, scaled, and updated independently.

---

## Current Implementation Status

✅ **Completed**:

- Blazor Web UI with truck dashboard
- PostgreSQL database with EF Core
- RabbitMQ messaging infrastructure
- Inventory Service (background worker)
- Truck Status Service (background worker with simulation)
- Docker containers for all services
- Kubernetes deployment manifests

🚧 **In Progress**:

- Job Workflow Service (assigns trucks to orders)

---

## Suggested Demo Flow

1. **Intro**: Explain why Kubernetes is a good home for .NET apps.
2. **Architecture Diagram**: Show Blazor → Services → RabbitMQ/PostgreSQL.
3. **Demo Part 1**: HTTP service scaling/failover with `TruckStatusService`.
4. **Demo Part 2**: Async job queue with RabbitMQ and `JobWorkflowService`.
5. **Wrap-Up**: Highlight Kubernetes strengths (resilience, scaling, observability).

---

## Next Steps for Implementation

- Scaffold Blazor front-end project.
- Create .NET API projects for:
  - `TruckStatusService`
  - `JobWorkflowService`
  - `InventoryService`
- Add RabbitMQ client integration.
- Add PostgreSQL EF Core integration.
- Write Kubernetes manifests:
  - Deployments
  - Services
  - ConfigMaps/Secrets
- Demonstrate scaling and rolling updates live.
