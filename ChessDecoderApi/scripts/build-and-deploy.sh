#!/bin/bash

# Chess Decoder API - Build and Deploy Script
# This script builds the .NET solution and deploys it with Docker

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT=${1:-development}
BUILD_CONFIG=${2:-Release}
DOCKER_COMPOSE_FILE="docker-compose.yml"

echo -e "${BLUE}🚀 Chess Decoder API - Build and Deploy Script${NC}"
echo -e "${BLUE}Environment: ${ENVIRONMENT}${NC}"
echo -e "${BLUE}Build Configuration: ${BUILD_CONFIG}${NC}"
echo ""

# Function to print colored output
print_status() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

# Check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."
    
    # Check if .NET SDK is installed
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK is not installed. Please install .NET 9.0 SDK first."
        exit 1
    fi
    
    # Check if Docker is installed
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed. Please install Docker Desktop first."
        exit 1
    fi
    
    # Check if Docker daemon is running
    if ! docker info &> /dev/null; then
        print_error "Docker daemon is not running. Please start Docker Desktop."
        exit 1
    fi
    
    print_status "Prerequisites check passed!"
}

# Clean previous builds
clean_builds() {
    print_info "Cleaning previous builds..."
    
    # Clean .NET build artifacts
    dotnet clean --configuration $BUILD_CONFIG
    rm -rf bin/ obj/ publish/
    
    # Clean Docker images (optional)
    if [[ "$ENVIRONMENT" == "production" ]]; then
        docker system prune -f
    fi
    
    print_status "Cleanup completed!"
}

# Build .NET solution
build_solution() {
    print_info "Building .NET solution..."
    
    # Restore packages
    dotnet restore
    
    # Build solution
    dotnet build --configuration $BUILD_CONFIG --no-restore
    
    # Run tests (optional)
    if [[ "$ENVIRONMENT" == "production" ]]; then
        print_info "Running tests..."
        dotnet test --configuration $BUILD_CONFIG --no-build --verbosity normal
    fi
    
    print_status "Solution built successfully!"
}

# Publish application
publish_application() {
    print_info "Publishing application..."
    
    # Create publish directory
    mkdir -p publish
    
    # Publish application
    dotnet publish \
        --configuration $BUILD_CONFIG \
        --output ./publish \
        --no-build \
        --verbosity normal
    
    print_status "Application published to ./publish/"
}

# Build Docker image
build_docker_image() {
    print_info "Building Docker image..."
    
    # Set Docker Compose file based on environment
    if [[ "$ENVIRONMENT" == "production" ]]; then
        DOCKER_COMPOSE_FILE="docker-compose.prod.yml"
        print_info "Using production configuration: $DOCKER_COMPOSE_FILE"
    fi
    
    # Build Docker image
    docker compose -f $DOCKER_COMPOSE_FILE build --no-cache
    
    print_status "Docker image built successfully!"
}

# Deploy with Docker Compose
deploy_docker() {
    print_info "Deploying with Docker Compose..."
    
    # Stop existing services
    print_info "Stopping existing services..."
    docker compose -f $DOCKER_COMPOSE_FILE down
    
    # Start services
    print_info "Starting services..."
    docker compose -f $DOCKER_COMPOSE_FILE up -d
    
    # Wait for services to be ready
    print_info "Waiting for services to be ready..."
    sleep 15
    
    # Check service health
    print_info "Checking service health..."
    docker compose -f $DOCKER_COMPOSE_FILE ps
    
    print_status "Deployment completed!"
}

# Show deployment info
show_deployment_info() {
    echo ""
    echo -e "${GREEN}🎉 Deployment completed successfully!${NC}"
    echo ""
    
    if [[ "$ENVIRONMENT" == "development" ]]; then
        echo "🌐 Services available at:"
        echo "  🚀 API: http://localhost:5100"
        echo "  🐘 PostgreSQL: localhost:5432"
        echo "  🔴 Redis: localhost:6379"
        echo "  🌐 pgAdmin: http://localhost:8080"
    else
        echo "🌐 Services available at:"
        echo "  🚀 API: http://localhost:80"
        echo "  🐘 PostgreSQL: localhost:5432"
        echo "  🔴 Redis: localhost:6379"
    fi
    
    echo ""
    echo "📋 Useful commands:"
    echo "  View logs: docker compose -f $DOCKER_COMPOSE_FILE logs -f"
    echo "  Stop services: docker compose -f $DOCKER_COMPOSE_FILE down"
    echo "  Restart services: docker compose -f $DOCKER_COMPOSE_FILE restart"
    echo ""
    echo "🧪 Test the API:"
    if [[ "$ENVIRONMENT" == "development" ]]; then
        echo "  curl http://localhost:5100/api/test/health"
    else
        echo "  curl http://localhost/api/test/health"
    fi
}

# Main execution
main() {
    echo -e "${BLUE}Starting build and deploy process...${NC}"
    echo ""
    
    # Execute steps
    check_prerequisites
    clean_builds
    build_solution
    publish_application
    build_docker_image
    deploy_docker
    show_deployment_info
}

# Handle command line arguments
case "$ENVIRONMENT" in
    "dev"|"development")
        ENVIRONMENT="development"
        ;;
    "prod"|"production")
        ENVIRONMENT="production"
        ;;
    *)
        print_warning "Unknown environment '$ENVIRONMENT'. Using 'development'."
        ENVIRONMENT="development"
        ;;
esac

# Run main function
main
