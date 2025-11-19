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

1. **TruckStatusService (HTTP)**
   - Provides current status of trucks.
   - Supports scaling replicas → demo load balancing and failover.
   - Blazor UI polls or subscribes for updates.

2. **JobWorkflowService (RabbitMQ)**
   - Consumes messages like `StartPouring`, `WashTruck`, `ReturnToPlant`.
   - Demonstrates async orchestration and resilience.
   - Worker pods can scale up/down to handle backlog.

3. **InventoryService (HTTP + PostgreSQL)**
   - Tracks materials (sand, rock, cement, water).
   - Updates inventory when trucks load materials.
   - Persists data in PostgreSQL.

### Database

- **PostgreSQL**
  - Stores truck states, delivery history, and material inventory.

---

## Kubernetes Demo Highlights

- **Replica Failover**: Scale `TruckStatusService` to multiple pods, kill one, show Blazor UI still works.
- **Async Messaging**: Queue multiple jobs in RabbitMQ, then scale worker pods to drain backlog.
- **Rolling Updates**: Deploy new version of `JobWorkflowService` with updated status messages.
- **Persistence**: PostgreSQL ensures history is intact even if services restart.

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
