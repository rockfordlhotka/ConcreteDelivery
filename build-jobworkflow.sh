#!/bin/bash

# Build script for Job Workflow Service Docker image

set -e

echo "Building Concrete Delivery Job Workflow Service Docker image..."

# Build the Docker image
docker build \
  -f src/ConcreteDelivery.JobWorkflowService/Dockerfile \
  -t rockylhotka/concretedelivery-jobworkflow:latest \
  .

echo "Build complete!"
echo ""
echo "To push to Docker Hub:"
echo "  docker push rockylhotka/concretedelivery-jobworkflow:latest"
echo ""
echo "To run locally:"
echo "  docker run --rm -it rockylhotka/concretedelivery-jobworkflow:latest"
