#!/bin/bash

# Chess Decoder API - Docker Setup Script (SQLite Version)
# This script sets up the API with SQLite database

set -e

echo "🐳 Setting up Chess Decoder API with Docker (SQLite)..."

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker Desktop first:"
    echo "   macOS: https://docs.docker.com/desktop/install/mac-install/"
    echo "   Windows: https://docs.docker.com/desktop/install/windows-install/"
    echo "   Linux: https://docs.docker.com/engine/install/"
    exit 1
fi

# Check if Docker Compose is available
if ! docker compose version &> /dev/null; then
    echo "❌ Docker Compose is not available. Please install Docker Compose:"
    echo "   https://docs.docker.com/compose/install/"
    exit 1
fi

# Check if Docker daemon is running
if ! docker info &> /dev/null; then
    echo "❌ Docker daemon is not running. Please start Docker Desktop."
    exit 1
fi

echo "✅ Docker is ready!"

# Create .env file for Docker
echo "📝 Creating .env file for Docker..."
cat > .env << ENVEOF
# Docker Environment Variables
ASPNETCORE_ENVIRONMENT=Development

# Redis Configuration (Docker)
REDIS_HOST=localhost
REDIS_PORT=6379

# SQLite Configuration
DEFAULT_CONNECTION_STRING=Data Source=data/chessdecoder.db
ENVEOF

echo "✅ .env file created!"

# Create data directory for SQLite
echo "📁 Creating data directory for SQLite..."
mkdir -p data
echo "✅ Data directory created!"

# Update appsettings.json with SQLite connection string
echo "📝 Updating appsettings.json..."
SQLITE_CONNECTION="Data Source=data/chessdecoder.db"

# Create backup of original file
cp appsettings.json appsettings.json.backup

# Update connection string
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s|Host=postgres;Port=5432;Database=chessdecoder;Username=chessdecoder_user;Password=chessdecoder_password_dev|$SQLITE_CONNECTION|g" appsettings.json
else
    # Linux
    sed -i "s|Host=postgres;Port=5432;Database=chessdecoder;Username=chessdecoder_user;Password=chessdecoder_password_dev|$SQLITE_CONNECTION|g" appsettings.json
fi

echo "✅ appsettings.json updated!"

# Start Docker services
echo "🚀 Starting Docker services..."
docker compose up -d

# Wait for services to be healthy
echo "⏳ Waiting for services to be ready..."
sleep 5

# Check service health
echo "🔍 Checking service health..."

# Check Redis
if docker compose exec redis redis-cli ping | grep -q "PONG"; then
    echo "✅ Redis is ready!"
else
    echo "❌ Redis is not ready. Check logs with: docker compose logs redis"
    exit 1
fi

echo ""
echo "🎉 Docker setup completed successfully!"
echo ""
echo "Services running:"
echo "  🔴 Redis: localhost:6379"
echo "  🚀 API: http://localhost:5100"
echo ""
echo "Database: SQLite (data/chessdecoder.db)"
echo ""
echo "Next steps:"
echo "1. Install EF Core tools: dotnet tool install --global dotnet-ef"
echo "2. Create migration: dotnet ef migrations add InitialCreate"
echo "3. Apply migration: dotnet ef database update"
echo "4. Start your application: dotnet run"
echo ""
echo "Useful Docker commands:"
echo "  Start services: docker compose up -d"
echo "  Stop services: docker compose down"
echo "  View logs: docker compose logs -f"
echo "  Reset Redis: docker compose down -v && docker compose up -d"
echo "  Access Redis: docker compose exec redis redis-cli"
