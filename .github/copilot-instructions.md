# Copilot Instructions

## Technology Stack

This project uses the following technologies and should follow their best practices:

- **.NET 10**: Use the latest .NET 10 features and patterns
- **Blazor**: UI framework for building interactive web applications
- **PostgreSQL**: Primary database for data persistence
- **RabbitMQ**: Message broker for asynchronous communication between services
- **Kubernetes (K3s)**: Deployment target using a personal K3s cluster
- **Service-Oriented Architecture**: Multiple services communicating via messages and APIs

## Architecture Guidelines

- Design services to be loosely coupled and independently deployable
- Use RabbitMQ for inter-service communication where appropriate
- Follow microservices patterns and principles
- Ensure services are container-ready and Kubernetes-compatible
- Use PostgreSQL for persistent storage with proper connection pooling
- Implement health checks for Kubernetes liveness and readiness probes

## Code Standards

- Follow C# and .NET best practices
- Use async/await patterns for I/O operations
- Implement proper error handling and logging
- Write testable code with dependency injection
- Use Entity Framework Core for database access when applicable
- Follow RESTful API design principles for service endpoints

## Deployment Considerations

- Generate Kubernetes manifests compatible with K3s
- All pods and services are deployed in the `concretedelivery` namespace in Kubernetes
- Include resource limits and requests in deployment specs
- Configure services for horizontal scaling where appropriate
- Use ConfigMaps and Secrets for configuration management
- **IMPORTANT**: After rebuilding any service Docker image, it MUST be pushed to Docker Hub before restarting the Kubernetes deployment. The deployment pulls images from Docker Hub, not from local Docker storage. Use `docker push rockylhotka/<service-name>:latest` after each build.
