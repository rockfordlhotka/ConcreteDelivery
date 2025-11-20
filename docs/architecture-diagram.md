# Concrete Delivery System - Architecture Diagram

This document contains two views of the system architecture:
1. **High-Level Service Interaction** - Shows service-to-service communication patterns
2. **Detailed Kubernetes Deployment** - Shows all infrastructure components and connections

## High-Level Service Interaction

This diagram focuses on the core services and their message-based communication patterns.

```mermaid
graph LR
    subgraph "User Interface"
        WEB[Blazor Web App<br/>Dispatcher Dashboard]
    end

    subgraph "Message Exchanges"
        CMD{{Commands<br/>concrete.commands}}
        EVT{{Events<br/>concrete.events}}
    end

    subgraph "Background Services"
        JOB[Job Workflow Service<br/><i>Orchestrator</i>]
        TRUCK[Truck Status Service<br/><i>Simulator</i>]
        INV[Inventory Service<br/><i>Event Listener</i>]
    end

    %% Command flows
    WEB -->|Dispatch Commands| CMD
    JOB -->|Truck Assignments| CMD
    CMD -->|Assign Truck| JOB
    CMD -->|Execute Operations| TRUCK

    %% Event flows
    TRUCK -->|Status Updates| EVT
    EVT -->|Inventory Events| INV
    EVT -->|Real-time Updates| WEB

    %% Styling
    classDef webApp fill:#4A90E2,stroke:#2E5C8A,color:#fff,stroke-width:3px
    classDef worker fill:#50C878,stroke:#2E7D52,color:#fff,stroke-width:2px
    classDef exchange fill:#9B59B6,stroke:#5B3566,color:#fff,stroke-width:2px

    class WEB webApp
    class JOB,TRUCK,INV worker
    class CMD,EVT exchange
```

### Message Flow Patterns

#### 1. Dispatcher Initiates Order
```
User → Web App → concrete.commands → Job Workflow Service
```

#### 2. Truck Assignment
```
Job Workflow Service → concrete.commands → Truck Status Service
```

#### 3. Truck Operations & Status Updates
```
Truck Status Service → concrete.events → Inventory Service (updates inventory)
                                       → Web App (updates dashboard)
```

### Service Responsibilities

| Service | Type | Purpose | Publishes | Consumes |
|---------|------|---------|-----------|----------|
| **Blazor Web App** | Web UI | Dispatcher dashboard, order management | Commands | Events |
| **Job Workflow Service** | Worker | Assigns trucks to orders, orchestrates workflow | Commands | Commands |
| **Truck Status Service** | Worker | Simulates truck operations through lifecycle | Events | Commands |
| **Inventory Service** | Worker | Manages plant material inventory | - | Events |

## Detailed Kubernetes Deployment

This diagram shows the components deployed to Kubernetes and how they interact.

```mermaid
graph TB
    subgraph "External Access"
        USER[Dispatcher/User]
        TAILSCALE[Tailscale Service]
    end

    subgraph "Kubernetes Cluster - concretedelivery namespace"
        subgraph "Web Layer"
            WEB[Blazor Web App<br/>concretedelivery-web<br/>Port: 8080]
        end

        subgraph "Background Workers"
            INVENTORY[Inventory Service<br/>concretedelivery-inventory<br/>Background Worker]
            TRUCKSTATUS[Truck Status Service<br/>concretedelivery-truckstatus<br/>Background Worker]
            JOBWORKFLOW[Job Workflow Service<br/>concretedelivery-jobworkflow<br/>Background Worker<br/><i>Planned</i>]
        end

        subgraph "Message Broker"
            RMQ[RabbitMQ<br/>External Service<br/>via Tailscale]
            CMDEX[Commands Exchange<br/>concrete.commands]
            EVTEX[Events Exchange<br/>concrete.events]
        end

        subgraph "Database"
            PG[(PostgreSQL<br/>External Service<br/>via Tailscale)]
        end

        subgraph "Kubernetes Secrets"
            DBSEC[concretedelivery-db-secret]
            RMQSEC[concretedelivery-rabbitmq-secret]
        end
    end

    %% User interactions
    USER -->|HTTPS| TAILSCALE
    TAILSCALE -->|HTTP| WEB

    %% Web layer interactions
    WEB -->|Reads/Writes| PG
    WEB -->|Publishes Commands| CMDEX
    WEB -->|Subscribes to Events| EVTEX

    %% Message flow
    CMDEX -->|Routes to| RMQ
    EVTEX -->|Routes to| RMQ
    RMQ -->|Delivers Commands| JOBWORKFLOW
    RMQ -->|Delivers Commands| TRUCKSTATUS
    RMQ -->|Delivers Events| INVENTORY
    RMQ -->|Delivers Events| WEB

    %% Worker service interactions
    INVENTORY -->|Reads/Writes| PG
    TRUCKSTATUS -->|Reads/Writes| PG
    TRUCKSTATUS -->|Publishes Events| EVTEX
    JOBWORKFLOW -->|Reads/Writes| PG
    JOBWORKFLOW -->|Publishes Commands| CMDEX

    %% Configuration
    DBSEC -.->|Connection String| WEB
    DBSEC -.->|Connection String| INVENTORY
    DBSEC -.->|Connection String| TRUCKSTATUS
    DBSEC -.->|Connection String| JOBWORKFLOW
    RMQSEC -.->|Credentials| WEB
    RMQSEC -.->|Credentials| INVENTORY
    RMQSEC -.->|Credentials| TRUCKSTATUS
    RMQSEC -.->|Credentials| JOBWORKFLOW

    %% Styling
    classDef webService fill:#4A90E2,stroke:#2E5C8A,color:#fff
    classDef worker fill:#50C878,stroke:#2E7D52,color:#fff
    classDef database fill:#E07B39,stroke:#8B4513,color:#fff
    classDef message fill:#9B59B6,stroke:#5B3566,color:#fff
    classDef secret fill:#95A5A6,stroke:#5D6D7E,color:#fff
    classDef external fill:#F39C12,stroke:#935A0A,color:#fff
    classDef planned fill:#BDC3C7,stroke:#7F8C8D,color:#333,stroke-dasharray: 5 5

    class WEB webService
    class INVENTORY,TRUCKSTATUS worker
    class JOBWORKFLOW planned
    class PG database
    class RMQ,CMDEX,EVTEX message
    class DBSEC,RMQSEC secret
    class TAILSCALE external
```

## Component Descriptions

### Web Layer
- **Blazor Web App**: Dispatcher dashboard for managing trucks and deliveries
  - HTTP service accessible via Tailscale
  - Publishes commands to orchestrate truck operations
  - Subscribes to real-time status events
  - Direct database access for queries and updates

### Background Workers
- **Inventory Service**: Event-driven inventory management
  - Listens for truck status events (e.g., materials loaded)
  - Deducts materials from plant inventory
  - Updates PostgreSQL inventory tables
  
- **Truck Status Service**: Simulates truck operations
  - Consumes truck assignment commands
  - Simulates complete truck workflow with compressed timing
  - Publishes status events at each phase
  - Updates truck state in PostgreSQL
  
- **Job Workflow Service** *(Planned)*: Orchestration service
  - Assigns trucks to delivery orders
  - Publishes truck assignment commands
  - Manages job lifecycle

### Infrastructure
- **RabbitMQ**: Message broker for async communication
  - **concrete.commands**: Command messages for truck operations
  - **concrete.events**: Event messages for status changes
  - Hosted externally, accessed via Tailscale network
  
- **PostgreSQL**: Persistent data storage
  - Truck states and delivery history
  - Material inventory
  - Order information
  - Hosted externally, accessed via Tailscale network

### Configuration
- **concretedelivery-db-secret**: Database connection strings
- **concretedelivery-rabbitmq-secret**: RabbitMQ credentials (host, username, password)

## Message Flow Examples

### Command Flow: Assign Truck to Job
```
Dispatcher (Web UI) → concrete.commands → Job Workflow Service
                                       ↓
                          concrete.commands → Truck Status Service
```

### Event Flow: Truck Materials Loaded
```
Truck Status Service → concrete.events → Inventory Service (deducts materials)
                                       → Web UI (updates dashboard)
```

## Kubernetes Features Demonstrated

- **Service Discovery**: Services communicate via RabbitMQ and PostgreSQL using DNS names
- **Configuration Management**: Secrets for sensitive data (database credentials, RabbitMQ passwords)
- **Resource Management**: CPU/memory requests and limits on all services
- **Health Checks**: Liveness and readiness probes ensure service availability
- **Scaling**: Horizontal scaling capability for all services
- **Rolling Updates**: Zero-downtime deployments with ImagePullPolicy: Always
- **Namespace Isolation**: All resources in dedicated `concretedelivery` namespace
