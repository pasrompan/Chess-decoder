# 🚀 Chess Decoder API - Build and Deploy Guide

This guide covers all the ways to build your .NET solution and deploy it with Docker.

## 🎯 **Quick Start (One Command)**

```bash
# Build and deploy everything with one command
./scripts/build-and-deploy.sh

# For production
./scripts/build-and-deploy.sh production
```

## 🏗️ **Build Options**

### **Option 1: Full Build and Deploy (Recommended)**
```bash
# Development environment
./scripts/build-and-deploy.sh development

# Production environment
./scripts/build-and-deploy.sh production

# Custom build configuration
./scripts/build-and-deploy.sh development Debug
```

### **Option 2: Manual Step-by-Step**
```bash
# 1. Build .NET solution
dotnet build --configuration Release

# 2. Publish application
dotnet publish --configuration Release --output ./publish

# 3. Build Docker image
docker compose build

# 4. Deploy with Docker
docker compose up -d
```

### **Option 3: Docker Only (No .NET Build)**
```bash
# Just build and run Docker containers
docker compose up -d --build
```

## 🐳 **Docker Deployment Methods**

### **Development Environment**
```bash
# Uses docker-compose.yml + docker-compose.override.yml
docker compose up -d --build

# Or use the script
./scripts/build-and-deploy.sh development
```

**Ports:**
- 🚀 API: `http://localhost:5100`
- 🐘 PostgreSQL: `localhost:5432`
- 🔴 Redis: `localhost:6379`
- 🌐 pgAdmin: `http://localhost:8080`

### **Production Environment**
```bash
# Uses docker-compose.prod.yml
docker compose -f docker-compose.prod.yml up -d --build

# Or use the script
./scripts/build-and-deploy.sh production
```

**Ports:**
- 🚀 API: `http://localhost:80`
- 🐘 PostgreSQL: `localhost:5432`
- 🔴 Redis: `localhost:6379`

## 🔧 **Build Script Details**

The `build-and-deploy.sh` script performs these steps:

1. **✅ Prerequisites Check**
   - .NET SDK availability
   - Docker installation and status

2. **🧹 Clean Previous Builds**
   - Removes `bin/`, `obj/`, `publish/` directories
   - Cleans Docker images (production only)

3. **🏗️ Build .NET Solution**
   - Restores NuGet packages
   - Builds solution in specified configuration
   - Runs tests (production only)

4. **📦 Publish Application**
   - Creates optimized production build
   - Outputs to `./publish/` directory

5. **🐳 Build Docker Image**
   - Multi-stage Docker build
   - Optimized for production

6. **🚀 Deploy with Docker**
   - Stops existing services
   - Starts new services
   - Health checks

## 📁 **File Structure**

```
├── docker-compose.yml              # Base configuration
├── docker-compose.override.yml     # Development overrides
├── docker-compose.prod.yml         # Production configuration
├── Dockerfile                      # Multi-stage build
├── scripts/
│   ├── build-and-deploy.sh        # Main build script
│   ├── docker-setup.sh            # Docker setup script
│   └── init-db.sql                # Database initialization
└── publish/                        # Published application (created during build)
```

## 🎛️ **Environment-Specific Configurations**

### **Development (`docker-compose.override.yml`)**
- Port 5100 for API (avoids conflicts)
- Development environment variables
- Volume mounts for live development
- pgAdmin for database management

### **Production (`docker-compose.prod.yml`)**
- Standard ports (80, 443)
- Production environment variables
- Resource limits and reservations
- Health checks and restart policies
- Optimized Redis configuration

## 🚀 **Deployment Workflows**

### **Local Development**
```bash
# Quick start for development
./scripts/build-and-deploy.sh development

# Or manual Docker only
docker compose up -d --build
```

### **Testing Production Build**
```bash
# Build and test production locally
./scripts/build-and-deploy.sh production

# Test production endpoints
curl http://localhost/api/test/health
```

### **Continuous Integration**
```bash
# In CI/CD pipeline
dotnet build --configuration Release
dotnet test --configuration Release
dotnet publish --configuration Release --output ./publish
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
```

## 🛠️ **Useful Commands**

### **Build Commands**
```bash
# Build specific configuration
dotnet build --configuration Debug
dotnet build --configuration Release

# Clean builds
dotnet clean
dotnet clean --configuration Release

# Restore packages
dotnet restore
```

### **Docker Commands**
```bash
# View running services
docker compose ps

# View logs
docker compose logs -f
docker compose logs -f api

# Stop services
docker compose down

# Rebuild specific service
docker compose build api
docker compose up -d api
```

### **Database Commands**
```bash
# Access PostgreSQL
docker compose exec postgres psql -U chessdecoder_user -d chessdecoder

# Access Redis
docker compose exec redis redis-cli

# Reset database
docker compose down -v
docker compose up -d
```

## 🔍 **Troubleshooting**

### **Build Issues**
```bash
# Clean everything and rebuild
dotnet clean
rm -rf bin/ obj/ publish/
dotnet restore
dotnet build
```

### **Docker Issues**
```bash
# Check Docker status
docker info
docker version

# Reset Docker
docker compose down -v
docker system prune -a
./scripts/build-and-deploy.sh
```

### **Port Conflicts**
```bash
# Check what's using a port
lsof -i :5100

# Kill process or change port in docker-compose files
```

## 📊 **Performance Optimization**

### **Build Performance**
```bash
# Parallel builds
dotnet build --max-concurrent-builds 4

# Skip restore if packages are up to date
dotnet build --no-restore
```

### **Docker Performance**
```bash
# Use BuildKit for faster builds
export DOCKER_BUILDKIT=1

# Multi-stage builds (already configured)
# Layer caching optimization
```

## 🚀 **Next Steps After Deployment**

1. **Test the API**
   ```bash
   curl http://localhost:5100/api/test/health
   ```

2. **Run Database Migrations**
   ```bash
   dotnet ef database update
   ```

3. **Monitor Services**
   ```bash
   docker compose ps
   docker compose logs -f
   ```

4. **Scale Services** (if needed)
   ```bash
   docker compose up -d --scale api=3
   ```

---

**🎉 You're ready to build and deploy!** The automated script handles everything, or you can use the manual commands for more control.
