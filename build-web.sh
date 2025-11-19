#!/bin/bash

# Build script for Web Application Docker image

set -e

echo "Building Concrete Delivery Web Application Docker image..."

# Build the Docker image
docker build \
  -f src/ConcreteDelivery.Web/Dockerfile \
  -t rockylhotka/concretedelivery-web:latest \
  .

echo "Build complete!"
echo ""
echo "To push to Docker Hub:"
echo "  docker push rockylhotka/concretedelivery-web:latest"
echo ""
echo "To run locally:"
echo "  docker run --rm -it -p 8080:8080 rockylhotka/concretedelivery-web:latest"
