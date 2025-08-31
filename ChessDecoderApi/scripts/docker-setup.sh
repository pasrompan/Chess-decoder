#!/bin/bash

# Chess Decoder API - Docker Database Setup Script
# This script sets up PostgreSQL using Docker Compose

set -e

echo "🐳 Setting up Chess Decoder API with Docker..."

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
cat > .env << EOF
# Docker Environment Variables
ASPNETCORE_ENVIRONMENT=Development

# Database Configuration (Docker)
DB_HOST=localhost
DB_PORT=5432
DB_NAME=chessdecoder
DB_USER=chessdecoder_user
DB_PASSWORD=chessdecoder_password

# Redis Configuration (Docker)
REDIS_HOST=localhost
REDIS_PORT=6379

# Connection Strings
DEFAULT_CONNECTION_STRING=Host=localhost;Port=5432;Database=chessdecoder;Username=chessdecoder_user;Password=chessdecoder_password
EOF

echo "✅ .env file created!"

# Update appsettings.json with Docker connection string
echo "📝 Updating appsettings.json..."
DOCKER_CONNECTION="Host=localhost;Port=5432;Database=chessdecoder;Username=chessdecoder_user;Password=chessdecoder_password"

# Create backup of original file
cp appsettings.json appsettings.json.backup

# Update connection string
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s|Host=localhost;Database=chessdecoder;Username=postgres;Password=your_password_here|$DOCKER_CONNECTION|g" appsettings.json
else
    # Linux
    sed -i "s|Host=localhost;Database=chessdecoder;Username=postgres;Password=your_password_here|$DOCKER_CONNECTION|g" appsettings.json
fi

echo "✅ appsettings.json updated!"

# Start Docker services
echo "🚀 Starting Docker services..."
docker compose up -d

# Wait for services to be healthy
echo "⏳ Waiting for services to be ready..."
sleep 10

# Check service health
echo "🔍 Checking service health..."

# Check PostgreSQL
if docker compose exec postgres pg_isready -U chessdecoder_user -d chessdecoder; then
    echo "✅ PostgreSQL is ready!"
else
    echo "❌ PostgreSQL is not ready. Check logs with: docker compose logs postgres"
    exit 1
fi

# Check Redis
if docker compose exec redis redis-cli ping | grep -q "PONG"; then
    echo "✅ Redis is ready!"
else
    echo "❌ Redis is not ready. Check logs with: docker compose logs redis"
    exit 1
fi

# Check pgAdmin
echo "✅ pgAdmin is starting (may take a moment to be fully ready)"

echo ""
echo "🎉 Docker setup completed successfully!"
echo ""
echo "Services running:"
echo "  🐘 PostgreSQL: localhost:5432"
echo "  🔴 Redis: localhost:6379"
echo "  🌐 pgAdmin: http://localhost:8081"
echo "  🚀 API: http://localhost:5100 (HTTP), https://localhost:5101 (HTTPS)"
echo ""
echo "Database credentials:"
echo "  Database: chessdecoder"
echo "  Username: chessdecoder_user"
echo "  Password: chessdecoder_password"
echo ""
echo "pgAdmin credentials:"
echo "  Email: admin@chessdecoder.com"
echo "  Password: admin123"
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
echo "  Reset database: docker compose down -v && docker compose up -d"
echo "  Access PostgreSQL: docker compose exec postgres psql -U chessdecoder_user -d chessdecoder"
