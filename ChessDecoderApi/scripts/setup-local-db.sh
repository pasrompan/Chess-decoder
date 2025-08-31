#!/bin/bash

# Chess Decoder API - Local Database Setup Script
# This script helps set up PostgreSQL for local development

set -e

echo "🚀 Setting up local PostgreSQL database for Chess Decoder API..."

# Check if PostgreSQL is installed
if ! command -v psql &> /dev/null; then
    echo "❌ PostgreSQL is not installed. Please install it first:"
    echo "   macOS: brew install postgresql@15"
    echo "   Ubuntu: sudo apt install postgresql postgresql-contrib"
    echo "   Windows: Download from https://www.postgresql.org/download/windows/"
    exit 1
fi

# Check if PostgreSQL service is running
if ! pg_isready -q; then
    echo "⚠️  PostgreSQL service is not running. Starting it..."
    
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        brew services start postgresql@15
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        # Linux
        sudo systemctl start postgresql
    else
        echo "❌ Please start PostgreSQL service manually for your OS"
        exit 1
    fi
    
    # Wait for service to be ready
    echo "⏳ Waiting for PostgreSQL to be ready..."
    sleep 3
fi

# Get database configuration from user
echo ""
echo "📝 Database Configuration:"
read -p "Database name [chessdecoder]: " DB_NAME
DB_NAME=${DB_NAME:-chessdecoder}

read -p "Username [chessdecoder_user]: " DB_USER
DB_USER=${DB_USER:-chessdecoder_user}

read -s -p "Password: " DB_PASSWORD
echo ""

read -s -p "Confirm password: " DB_PASSWORD_CONFIRM
echo ""

if [ "$DB_PASSWORD" != "$DB_PASSWORD_CONFIRM" ]; then
    echo "❌ Passwords do not match!"
    exit 1
fi

echo ""
echo "🔧 Creating database and user..."

# Create database and user
sudo -u postgres psql << EOF
CREATE DATABASE $DB_NAME;
CREATE USER $DB_USER WITH PASSWORD '$DB_PASSWORD';
GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;
ALTER USER $DB_USER CREATEDB;
\q
EOF

echo "✅ Database and user created successfully!"

# Test connection
echo "🧪 Testing database connection..."
PGPASSWORD=$DB_PASSWORD psql -h localhost -U $DB_USER -d $DB_NAME -c "SELECT version();" > /dev/null 2>&1

if [ $? -eq 0 ]; then
    echo "✅ Database connection successful!"
else
    echo "❌ Database connection failed!"
    exit 1
fi

# Update appsettings.json
echo "📝 Updating appsettings.json..."
CONNECTION_STRING="Host=localhost;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

# Create backup of original file
cp appsettings.json appsettings.json.backup

# Update connection string
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s|Host=localhost;Database=chessdecoder;Username=postgres;Password=your_password_here|$CONNECTION_STRING|g" appsettings.json
else
    # Linux
    sed -i "s|Host=localhost;Database=chessdecoder;Username=postgres;Password=your_password_here|$CONNECTION_STRING|g" appsettings.json
fi

echo "✅ appsettings.json updated!"

# Create .env file
echo "📝 Creating .env file..."
cat > .env << EOF
# Database Configuration
DB_HOST=localhost
DB_NAME=$DB_NAME
DB_USER=$DB_USER
DB_PASSWORD=$DB_PASSWORD

# Environment
ASPNETCORE_ENVIRONMENT=Development
EOF

echo "✅ .env file created!"

# Install EF Core tools if not present
if ! command -v dotnet-ef &> /dev/null; then
    echo "🔧 Installing Entity Framework Core tools..."
    dotnet tool install --global dotnet-ef
fi

echo ""
echo "🎉 Local database setup completed successfully!"
echo ""
echo "Next steps:"
echo "1. Run: dotnet ef migrations add InitialCreate"
echo "2. Run: dotnet ef database update"
echo "3. Start your application: dotnet run"
echo ""
echo "Database connection details:"
echo "  Host: localhost"
echo "  Database: $DB_NAME"
echo "  Username: $DB_USER"
echo "  Password: [hidden]"
echo ""
echo "Files created/modified:"
echo "  ✅ appsettings.json (updated with connection string)"
echo "  ✅ .env (created with environment variables)"
echo "  ✅ appsettings.json.backup (backup of original file)"
