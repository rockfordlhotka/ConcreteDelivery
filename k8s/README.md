# Kubernetes Deployment Guide

This directory contains Kubernetes manifests and scripts for deploying ConcreteDelivery applications to your K3s cluster.

## Prerequisites

- K3s cluster running and accessible via `kubectl`
- Tailscale operator installed in the cluster
- Docker installed locally for building images
- Docker Hub account (or modify for your registry)

## Directory Structure

```
k8s/
├── create-secrets.sh              # Script to create K8s secrets
├── web-deployment.yaml            # Web app deployment and ClusterIP service
├── web-tailscale-service.yaml     # Tailscale LoadBalancer service
└── README.md                      # This file
```

## Deployment Steps

### 1. Build and Push the Docker Image

From the repository root:

```bash
# Build the image (from repo root, not src/ConcreteDelivery.Web)
docker build -f src/ConcreteDelivery.Web/Dockerfile -t rockylhotka/concretedelivery-web:latest .

# Push to Docker Hub
docker push rockylhotka/concretedelivery-web:latest
```

### 2. Create Kubernetes Secrets

Run the secret creation script to set up database and RabbitMQ credentials:

```bash
# Make the script executable
chmod +x k8s/create-secrets.sh

# Run the script
./k8s/create-secrets.sh
```

This creates:
- `concretedelivery` namespace
- `concretedelivery-db-secret` - PostgreSQL connection string
- `concretedelivery-rabbitmq-secret` - RabbitMQ credentials

**Important:** The secrets contain your actual credentials. Never commit the `create-secrets.sh` file with real credentials to version control. Consider using a secure secrets management solution for production.

### 3. Deploy the Web Application

```bash
# Apply the deployment and service
kubectl apply -f k8s/web-deployment.yaml

# Verify the deployment
kubectl get deployments -n concretedelivery
kubectl get pods -n concretedelivery
```

### 4. Expose via Tailscale

```bash
# Apply the Tailscale service
kubectl apply -f k8s/web-tailscale-service.yaml

# Check the service status
kubectl get svc -n concretedelivery

# Get the Tailscale hostname
kubectl get svc concretedelivery-web-tailscale -n concretedelivery -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'
```

The web application will be accessible at `https://concretedelivery` in your Tailnet.

## Configuration

### Resource Limits

The web application deployment has the following resource configuration:

- **Requests:** 200m CPU, 512Mi memory
- **Limits:** 1000m CPU, 1Gi memory

Adjust these in `web-deployment.yaml` if needed for your cluster.

### Replicas

The deployment runs 2 replicas by default for high availability. Modify the `replicas` field in `web-deployment.yaml` to adjust.

### Environment Variables

The following environment variables are configured from secrets:

- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `RabbitMq__Server` - RabbitMQ host
- `RabbitMq__User` - RabbitMQ username
- `RabbitMq__Password` - RabbitMQ password

## Health Checks

The deployment includes:

- **Liveness Probe:** HTTP GET on `/` every 10s (after 30s initial delay)
- **Readiness Probe:** HTTP GET on `/` every 5s (after 10s initial delay)

## Troubleshooting

### Check Pod Logs

```bash
kubectl logs -n concretedelivery -l app=concretedelivery-web --tail=100 -f
```

### Check Pod Status

```bash
kubectl describe pod -n concretedelivery -l app=concretedelivery-web
```

### Check Secrets

```bash
kubectl get secrets -n concretedelivery
kubectl describe secret concretedelivery-db-secret -n concretedelivery
```

### Restart Deployment

```bash
kubectl rollout restart deployment/concretedelivery-web -n concretedelivery
```

### Access Pod Shell

```bash
kubectl exec -it -n concretedelivery deployment/concretedelivery-web -- /bin/bash
```

## Updating the Application

To deploy a new version:

```bash
# Build and push new image
docker build -f src/ConcreteDelivery.Web/Dockerfile -t rockylhotka/concretedelivery-web:latest .
docker push rockylhotka/concretedelivery-web:latest

# Force pods to pull new image
kubectl rollout restart deployment/concretedelivery-web -n concretedelivery

# Watch the rollout
kubectl rollout status deployment/concretedelivery-web -n concretedelivery
```

## Cleanup

To remove the deployment:

```bash
kubectl delete -f k8s/web-tailscale-service.yaml
kubectl delete -f k8s/web-deployment.yaml
kubectl delete secret concretedelivery-db-secret concretedelivery-rabbitmq-secret -n concretedelivery
kubectl delete namespace concretedelivery
```

## Notes

- The Dockerfile is located in `src/ConcreteDelivery.Web/Dockerfile` but must be built from the repository root
- HTTPS termination is handled by Tailscale
- The application runs on port 8080 internally
- All credentials are stored in Kubernetes secrets, not in code
