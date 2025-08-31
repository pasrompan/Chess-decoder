#!/bin/bash

# Chess Decoder API - Google Cloud Database Setup Script
# This script helps set up PostgreSQL on Google Cloud

set -e

echo "☁️  Setting up Google Cloud PostgreSQL for Chess Decoder API..."

# Check if gcloud is installed
if ! command -v gcloud &> /dev/null; then
    echo "❌ Google Cloud CLI is not installed. Please install it first:"
    echo "   macOS: brew install google-cloud-sdk"
    echo "   Linux: curl https://sdk.cloud.google.com | bash"
    echo "   Windows: Download from https://cloud.google.com/sdk/docs/install"
    exit 1
fi

# Check if user is authenticated
if ! gcloud auth list --filter=status:ACTIVE --format="value(account)" | grep -q .; then
    echo "🔐 Please authenticate with Google Cloud first:"
    echo "   gcloud auth login"
    exit 1
fi

# Get project configuration
echo ""
echo "📝 Google Cloud Configuration:"

# Get current project
CURRENT_PROJECT=$(gcloud config get-value project 2>/dev/null)
if [ -z "$CURRENT_PROJECT" ]; then
    echo "❌ No project is set. Please set a project first:"
    echo "   gcloud config set project YOUR_PROJECT_ID"
    exit 1
fi

echo "Current project: $CURRENT_PROJECT"
read -p "Use this project? (y/n) [y]: " USE_CURRENT
USE_CURRENT=${USE_CURRENT:-y}

if [[ "$USE_CURRENT" =~ ^[Nn]$ ]]; then
    read -p "Enter project ID: " PROJECT_ID
    gcloud config set project $PROJECT_ID
    CURRENT_PROJECT=$PROJECT_ID
fi

# Get region
read -p "Region [us-central1]: " REGION
REGION=${REGION:-us-central1}

# Get instance name
read -p "Instance name [chess-decoder-db]: " INSTANCE_NAME
INSTANCE_NAME=${INSTANCE_NAME:-chess-decoder-db}

# Get database name
read -p "Database name [chessdecoder]: " DB_NAME
DB_NAME=${DB_NAME:-chessdecoder}

# Get username
read -p "Database username [chessdecoder_user]: " DB_USER
DB_USER=${DB_USER:-chessdecoder_user}

# Get password
read -s -p "Database password: " DB_PASSWORD
echo ""

read -s -p "Confirm password: " DB_PASSWORD_CONFIRM
echo ""

if [ "$DB_PASSWORD" != "$DB_PASSWORD_CONFIRM" ]; then
    echo "❌ Passwords do not match!"
    exit 1
fi

echo ""
echo "🔧 Setting up Google Cloud PostgreSQL..."

# Enable required APIs
echo "📡 Enabling required APIs..."
gcloud services enable sqladmin.googleapis.com
gcloud services enable cloudbuild.googleapis.com

# Create Cloud SQL instance
echo "🏗️  Creating Cloud SQL instance..."
gcloud sql instances create $INSTANCE_NAME \
    --database-version=POSTGRES_15 \
    --tier=db-f1-micro \
    --region=$REGION \
    --storage-type=SSD \
    --storage-size=10GB \
    --backup-start-time=02:00 \
    --maintenance-window-day=SUN \
    --maintenance-window-hour=03:00 \
    --root-password=$DB_PASSWORD

# Create database
echo "🗄️  Creating database..."
gcloud sql databases create $DB_NAME --instance=$INSTANCE_NAME

# Create user
echo "👤 Creating database user..."
gcloud sql users create $DB_USER \
    --instance=$INSTANCE_NAME \
    --password=$DB_PASSWORD

# Get connection info
echo "🔍 Getting connection information..."
CONNECTION_NAME=$(gcloud sql instances describe $INSTANCE_NAME --format="value(connectionName)")

echo ""
echo "✅ Google Cloud PostgreSQL setup completed successfully!"
echo ""
echo "Connection Information:"
echo "  Project ID: $CURRENT_PROJECT"
echo "  Region: $REGION"
echo "  Instance: $INSTANCE_NAME"
echo "  Database: $DB_NAME"
echo "  Username: $DB_USER"
echo "  Connection Name: $CONNECTION_NAME"
echo ""

# Update appsettings.json
echo "📝 Updating appsettings.json..."
GOOGLE_CLOUD_CONNECTION="Host=/cloudsql/$CONNECTION_NAME;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

# Create backup of original file
cp appsettings.json appsettings.json.backup

# Update connection string
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s|Host=/cloudsql/PROJECT_ID:REGION:INSTANCE_NAME;Database=chessdecoder;Username=postgres;Password=your_password_here|$GOOGLE_CLOUD_CONNECTION|g" appsettings.json
else
    # Linux
    sed -i "s|Host=/cloudsql/PROJECT_ID:REGION:INSTANCE_NAME;Database=chessdecoder;Username=postgres;Password=your_password_here|$GOOGLE_CLOUD_CONNECTION|g" appsettings.json
fi

echo "✅ appsettings.json updated!"

# Update .env file
echo "📝 Updating .env file..."
cat >> .env << EOF

# Google Cloud Configuration
GOOGLE_CLOUD_PROJECT=$CURRENT_PROJECT
GOOGLE_CLOUD_REGION=$REGION
GOOGLE_CLOUD_SQL_INSTANCE=$INSTANCE_NAME
GOOGLE_CLOUD_CONNECTION_NAME=$CONNECTION_NAME
EOF

echo "✅ .env file updated!"

echo ""
echo "🎉 Google Cloud database setup completed!"
echo ""
echo "Next steps:"
echo "1. Deploy your application to Google Cloud"
echo "2. Set ASPNETCORE_ENVIRONMENT=Production"
echo "3. Run migrations: dotnet ef database update"
echo ""
echo "Important notes:"
echo "  • Instance tier: db-f1-micro (free tier eligible)"
echo "  • Storage: 10GB SSD"
echo "  • Backups: Daily at 2:00 AM"
echo "  • Maintenance: Sundays at 3:00 AM"
echo ""
echo "Files modified:"
echo "  ✅ appsettings.json (updated with Google Cloud connection)"
echo "  ✅ .env (updated with Google Cloud configuration)"
echo "  ✅ appsettings.json.backup (backup of original file)"
echo ""
echo "To connect from your local machine (for development):"
echo "  gcloud sql connect $INSTANCE_NAME --user=postgres"
