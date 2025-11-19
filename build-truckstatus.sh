#!/bin/bash

# Build script for Truck Status Service Docker image

set -e

echo "Building Concrete Delivery Truck Status Service Docker image..."

# Build the Docker image
docker build \
  -f src/ConcreteDelivery.TruckStatusService/Dockerfile \
  -t rockylhotka/concretedelivery-truckstatus:latest \
  .

echo "Build complete!"
echo ""
echo "To push to Docker Hub:"
echo "  docker push rockylhotka/concretedelivery-truckstatus:latest"
echo ""
echo "To run locally:"
echo "  docker run --rm -it rockylhotka/concretedelivery-truckstatus:latest"
