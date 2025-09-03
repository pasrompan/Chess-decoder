#!/bin/bash

# Chess Decoder API - Google Cloud Setup Script
set -e

echo "☁️ Setting up Chess Decoder API on Google Cloud..."

# Check if gcloud is installed
if ! command -v gcloud &> /dev/null; then
    echo "❌ gcloud CLI not found. Please install it first:"
    echo "   https://cloud.google.com/sdk/docs/install"
    exit 1
fi

# Get project ID
PROJECT_ID=$(gcloud config get-value project 2>/dev/null)
if [ -z "$PROJECT_ID" ]; then
    echo "❌ No project ID set. Please run:"
    echo "   gcloud config set project YOUR_PROJECT_ID"
    exit 1
fi

echo "📁 Project ID: $PROJECT_ID"

# Enable required APIs
echo "🔌 Enabling required APIs..."
gcloud services enable cloudbuild.googleapis.com
gcloud services enable run.googleapis.com
gcloud services enable storage.googleapis.com
gcloud services enable secretmanager.googleapis.com

# Create buckets
echo "🪣 Creating Cloud Storage buckets..."

# Database bucket
DB_BUCKET="chessdecoder-db-$PROJECT_ID"
gcloud storage buckets create gs://$DB_BUCKET \
    --project=$PROJECT_ID \
    --location=US \
    --uniform-bucket-level-access

# Images bucket
IMAGES_BUCKET="chessdecoder-images-$PROJECT_ID"
gcloud storage buckets create gs://$IMAGES_BUCKET \
    --project=$PROJECT_ID \
    --location=US \
    --uniform-bucket-level-access

# Make images bucket public for CDN
gcloud storage buckets add-iam-policy-binding gs://$IMAGES_BUCKET \
    --member="allUsers" \
    --role="roles/storage.objectViewer"

# Set default object ACL for the images bucket to make new objects public
gcloud storage buckets update gs://$IMAGES_BUCKET \
    --default-object-acl=public-read

# Create secrets
echo "🔐 Creating secrets..."
echo -n "$(read -p "Enter OpenAI API Key: " -s)" | gcloud secrets create openai-api-key --data-file=-
echo -n "$(read -p "Enter Google Vision API Key: " -s)" | gcloud secrets create google-vision-api-key --data-file=-
echo -n "$(read -p "Enter Google Client ID: " -s)" | gcloud secrets create google-client-id --data-file=-

# Grant Cloud Run access to secrets
echo "🔑 Granting Cloud Run access to secrets..."
gcloud secrets add-iam-policy-binding openai-api-key \
    --member="serviceAccount:$PROJECT_ID@appspot.gserviceaccount.com" \
    --role="roles/secretmanager.secretAccessor"

gcloud secrets add-iam-policy-binding google-vision-api-key \
    --member="serviceAccount:$PROJECT_ID@appspot.gserviceaccount.com" \
    --role="roles/secretmanager.secretAccessor"

gcloud secrets add-iam-policy-binding google-client-id \
    --member="serviceAccount:$PROJECT_ID@appspot.gserviceaccount.com" \
    --role="roles/secretmanager.secretAccessor"

# Grant Cloud Run access to buckets
echo "🪣 Granting Cloud Run access to buckets..."
gcloud storage buckets add-iam-policy-binding gs://$DB_BUCKET \
    --member="serviceAccount:$PROJECT_ID@appspot.gserviceaccount.com" \
    --role="roles/storage.objectAdmin"

gcloud storage buckets add-iam-policy-binding gs://$IMAGES_BUCKET \
    --member="serviceAccount:$PROJECT_ID@appspot.gserviceaccount.com" \
    --role="roles/storage.objectAdmin"

echo "✅ Google Cloud setup complete!"
echo ""
echo "📝 Next steps:"
echo "1. Update appsettings.Production.json with bucket names:"
echo "   - DatabaseBucketName: $DB_BUCKET"
echo "   - ImagesBucketName: $IMAGES_BUCKET"
echo ""
echo "2. Deploy to Cloud Run:"
echo "   gcloud run deploy chess-decoder-api --source ."
echo ""
echo "3. Or use Cloud Build:"
echo "   gcloud builds submit --config cloudbuild.yaml"
echo ""
echo "💰 Estimated monthly cost: ~$5-15 (depending on usage)"
