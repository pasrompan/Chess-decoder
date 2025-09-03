# Database Setup Guide for Chess Decoder API

This guide will help you set up PostgreSQL database for both local development and Google Cloud deployment.

## Local Development Setup

### 1. Install PostgreSQL

#### macOS (using Homebrew):
```bash
brew install postgresql@15
brew services start postgresql@15
```

#### Windows:
Download and install from [PostgreSQL official website](https://www.postgresql.org/download/windows/)

#### Linux (Ubuntu/Debian):
```bash
sudo apt update
sudo apt install postgresql postgresql-contrib
sudo systemctl start postgresql
sudo systemctl enable postgresql
```

### 2. Create Database and User

```bash
# Connect to PostgreSQL as superuser
sudo -u postgres psql

# Create database
CREATE DATABASE chessdecoder;

# Create user (replace 'your_username' and 'your_password' with desired values)
CREATE USER your_username WITH PASSWORD 'your_password';

# Grant privileges
GRANT ALL PRIVILEGES ON DATABASE chessdecoder TO your_username;

# Exit
\q
```

### 3. Update Connection String

Update `appsettings.json` with your local database credentials:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=chessdecoder;Username=your_username;Password=your_password"
  }
}
```

### 4. Run Entity Framework Migrations

```bash
# Install EF Core tools globally (if not already installed)
dotnet tool install --global dotnet-ef

# Create initial migration
dotnet ef migrations add InitialCreate

# Apply migration to database
dotnet ef database update
```

## Google Cloud Setup

### 1. Enable Required APIs

```bash
# Enable Cloud SQL Admin API
gcloud services enable sqladmin.googleapis.com

# Enable Cloud Build API (if using Cloud Build)
gcloud services enable cloudbuild.googleapis.com
```

### 2. Create Cloud SQL Instance

```bash
# Create PostgreSQL instance
gcloud sql instances create chess-decoder-db \
    --database-version=POSTGRES_15 \
    --tier=db-f1-micro \
    --region=us-central1 \
    --storage-type=SSD \
    --storage-size=10GB \
    --backup-start-time=02:00 \
    --maintenance-window-day=SUN \
    --maintenance-window-hour=03:00

# Create database
gcloud sql databases create chessdecoder --instance=chess-decoder-db

# Create user
gcloud sql users create chessdecoder_user \
    --instance=chess-decoder-db \
    --password=your_secure_password_here
```

### 3. Configure Connection

#### For App Engine/Cloud Run:
```bash
# Get connection info
gcloud sql instances describe chess-decoder-db --format="value(connectionName)"

# Update appsettings.json with the connection name
```

#### For Compute Engine/VM:
```bash
# Authorize your VM's IP
gcloud sql instances patch chess-decoder-db \
    --authorized-networks=YOUR_VM_IP/32
```

### 4. Update Production Connection String

Update `appsettings.json` with your Google Cloud connection:

```json
{
  "ConnectionStrings": {
    "GoogleCloudConnection": "Host=/cloudsql/PROJECT_ID:REGION:INSTANCE_NAME;Database=chessdecoder;Username=chessdecoder_user;Password=your_secure_password_here"
  }
}
```

Replace:
- `PROJECT_ID`: Your Google Cloud project ID
- `REGION`: The region where you created the instance (e.g., us-central1)
- `INSTANCE_NAME`: The name of your Cloud SQL instance (e.g., chess-decoder-db)

## Environment Variables

Create a `.env` file in your project root:

```env
# Database
DB_HOST=localhost
DB_NAME=chessdecoder
DB_USER=your_username
DB_PASSWORD=your_password

# Google Cloud (for production)
GOOGLE_CLOUD_PROJECT=your-project-id
GOOGLE_CLOUD_REGION=us-central1
GOOGLE_CLOUD_SQL_INSTANCE=chess-decoder-db
```

## Running Migrations

### Local Development:
```bash
dotnet ef database update
```

### Production (Google Cloud):
```bash
# Set environment to production
export ASPNETCORE_ENVIRONMENT=Production

# Run migrations
dotnet ef database update
```

## Database Schema Overview

### Tables:
1. **Users** - User accounts with credits
2. **ChessGames** - Processed chess games
3. **GameImages** - Input images for each game
4. **GameStatistics** - Game analysis statistics

### Key Features:
- User credit management
- Image storage tracking
- Game processing history
- Performance statistics
- Automatic cleanup (cascade deletes)

## Troubleshooting

### Common Issues:

1. **Connection Refused**: Ensure PostgreSQL is running
2. **Authentication Failed**: Check username/password in connection string
3. **Migration Errors**: Ensure database exists and user has proper permissions
4. **Google Cloud Connection**: Verify instance name and connection string format

### Useful Commands:

```bash
# Check PostgreSQL status
brew services list | grep postgresql

# Connect to database
psql -h localhost -U your_username -d chessdecoder

# List databases
\l

# List tables
\dt

# Check user permissions
\du
```

## Security Considerations

1. **Never commit passwords** to version control
2. **Use strong passwords** for production databases
3. **Limit network access** to database instances
4. **Enable SSL** for production connections
5. **Regular backups** of production data
6. **Monitor access logs** for suspicious activity

## Next Steps

After setting up the database:

1. Test the connection by running the application
2. Verify tables are created correctly
3. Test user registration and credit management
4. Implement image upload and game processing
5. Add monitoring and logging for production use
