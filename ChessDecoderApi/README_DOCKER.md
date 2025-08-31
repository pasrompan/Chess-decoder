# 🐳 Chess Decoder API - Docker Setup Guide

This guide shows you how to set up and run the Chess Decoder API using Docker, which is much more reliable and consistent than local installations.

## 🚀 Quick Start

### 1. Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- [Git](https://git-scm.com/) for cloning the repository

### 2. One-Command Setup
```bash
# Run the automated Docker setup script
./scripts/docker-setup.sh
```

### 3. Manual Setup (Alternative)
```bash
# Start all services
docker compose up -d

# Check service status
docker compose ps

# View logs
docker compose logs -f
```

## 🏗️ What Gets Set Up

### Services
- **🐘 PostgreSQL 15**: Main database
- **🔴 Redis 7**: Caching layer
- **🌐 pgAdmin**: Database management interface
- **🚀 API**: Your .NET application (in development mode)

### Ports
- **PostgreSQL**: `localhost:5432`
- **Redis**: `localhost:6379`
- **pgAdmin**: `http://localhost:8081`
- **API**: `http://localhost:5100` (HTTP), `https://localhost:5101` (HTTPS)

## 📁 File Structure

```
├── docker-compose.yml              # Main Docker Compose configuration
├── docker-compose.override.yml     # Development overrides
├── Dockerfile                      # Multi-stage API build
├── .dockerignore                   # Files to exclude from Docker build
├── scripts/
│   ├── docker-setup.sh            # Automated setup script
│   └── init-db.sql                # Database initialization
└── uploads/                        # File upload directory
    └── outputs/                    # Generated files directory
```

## 🔧 Configuration

### Environment Variables
The setup script creates a `.env` file with:
```env
ASPNETCORE_ENVIRONMENT=Development
DB_HOST=localhost
DB_PORT=5432
DB_NAME=chessdecoder
DB_USER=chessdecoder_user
DB_PASSWORD=chessdecoder_password
REDIS_HOST=localhost
REDIS_PORT=6379
```

### Database Credentials
- **Database**: `chessdecoder`
- **Username**: `chessdecoder_user`
- **Password**: `chessdecoder_password`

### pgAdmin Access
- **URL**: http://localhost:8080
- **Email**: `admin@chessdecoder.com`
- **Password**: `admin123`

## 🎯 Development Workflow

### 1. Start Services
```bash
docker compose up -d
```

### 2. Run Migrations
```bash
# Install EF Core tools (if not already installed)
dotnet tool install --global dotnet-ef

# Create initial migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update
```

### 3. Start API
```bash
dotnet run
```

### 4. Test Setup
Visit: `http://localhost:5100/api/test/health`

## 🛠️ Useful Docker Commands

### Service Management
```bash
# Start all services
docker compose up -d

# Stop all services
docker compose down

# Restart specific service
docker compose restart postgres

# View running services
docker compose ps
```

### Logs and Debugging
```bash
# View all logs
docker compose logs -f

# View specific service logs
docker compose logs -f postgres
docker compose logs -f api

# Access PostgreSQL directly
docker compose exec postgres psql -U chessdecoder_user -d chessdecoder

# Access Redis CLI
docker compose exec redis redis-cli
```

### Database Operations
```bash
# Reset database (removes all data)
docker compose down -v
docker compose up -d

# Backup database
docker compose exec postgres pg_dump -U chessdecoder_user chessdecoder > backup.sql

# Restore database
docker compose exec -T postgres psql -U chessdecoder_user -d chessdecoder < backup.sql
```

### Development
```bash
# Rebuild API container
docker compose build api

# Rebuild and restart API
docker compose up -d --build api

# View API container details
docker compose exec api dotnet --info
```

## 🔍 Troubleshooting

### Common Issues

#### 1. Port Already in Use
```bash
# Check what's using the port
lsof -i :5432

# Kill the process or change ports in docker-compose.yml
```

#### 2. Database Connection Issues
```bash
# Check if PostgreSQL is running
docker compose exec postgres pg_isready -U chessdecoder_user -d chessdecoder

# Check logs
docker compose logs postgres
```

#### 3. Permission Issues
```bash
# Fix file permissions
sudo chown -R $USER:$USER uploads/ outputs/

# Or run with proper user
docker compose exec -u root api chown -R 1000:1000 /app/uploads /app/outputs
```

#### 4. Memory Issues
```bash
# Check Docker resource usage
docker stats

# Increase Docker Desktop memory limit in preferences
```

### Health Checks
```bash
# Check all service health
docker compose ps

# Check specific service
docker compose exec postgres pg_isready -U chessdecoder_user -d chessdecoder
docker compose exec redis redis-cli ping
```

## 🚀 Production Considerations

### Security
- Change default passwords
- Use environment variables for secrets
- Enable SSL/TLS
- Restrict network access

### Performance
- Use production-grade PostgreSQL instance
- Configure connection pooling
- Set appropriate memory limits
- Enable query optimization

### Monitoring
- Set up logging aggregation
- Monitor resource usage
- Set up alerts for failures
- Regular backup verification

## 📚 Additional Resources

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [PostgreSQL Docker Image](https://hub.docker.com/_/postgres)
- [Redis Docker Image](https://hub.docker.com/_/redis)
- [pgAdmin Docker Image](https://hub.docker.com/r/dpage/pgadmin4)

## 🆘 Getting Help

### Check Service Status
```bash
docker compose ps
docker compose logs
```

### Reset Everything
```bash
# Stop and remove everything
docker compose down -v

# Remove all images
docker system prune -a

# Start fresh
./scripts/docker-setup.sh
```

### Debug Mode
```bash
# Run with verbose logging
docker compose up --verbose

# Check Docker daemon
docker info
docker version
```

---

**🎉 You're all set!** The Docker setup provides a consistent, reliable development environment that's easy to manage and reset when needed.
