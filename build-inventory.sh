#!/bin/bash

# Build script for ConcreteDelivery.InventoryService Docker image

set -e

IMAGE_NAME="rockylhotka/concretedelivery-inventory"
TAG="${1:-latest}"

echo "Building Docker image: ${IMAGE_NAME}:${TAG}"

# Build the Docker image from repository root
docker build -f src/ConcreteDelivery.InventoryService/Dockerfile -t ${IMAGE_NAME}:${TAG} .

echo "Build complete: ${IMAGE_NAME}:${TAG}"
echo ""
echo "To push to registry, run:"
echo "  docker push ${IMAGE_NAME}:${TAG}"
echo ""
echo "To run locally, run:"
echo "  docker run --rm -it ${IMAGE_NAME}:${TAG}"
