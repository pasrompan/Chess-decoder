#!/bin/bash

# Script to fix Gemini API key secret configuration
set -e

echo "🔧 Fixing Gemini API Key Secret Configuration..."

# Get project ID
PROJECT_ID=$(gcloud config get-value project 2>/dev/null)
if [ -z "$PROJECT_ID" ]; then
    echo "❌ No project ID set. Please run:"
    echo "   gcloud config set project YOUR_PROJECT_ID"
    exit 1
fi

echo "📁 Project ID: $PROJECT_ID"

# Check if secret exists
echo "🔍 Checking if GEMINI_API_KEY secret exists..."
if ! gcloud secrets describe GEMINI_API_KEY &>/dev/null; then
    echo "❌ Secret 'GEMINI_API_KEY' does not exist!"
    echo ""
    echo "Please create it with:"
    echo "  echo -n 'YOUR_GEMINI_API_KEY' | gcloud secrets create GEMINI_API_KEY --data-file=-"
    exit 1
fi

echo "✅ Secret 'GEMINI_API_KEY' exists"

# Get the service account
SERVICE_ACCOUNT=$(gcloud run services describe chessdecoder --region=europe-west1 --format="value(spec.template.spec.serviceAccountName)" 2>/dev/null || echo "")
if [ -z "$SERVICE_ACCOUNT" ]; then
    SERVICE_ACCOUNT="$PROJECT_ID-compute@developer.gserviceaccount.com"
    echo "⚠️  Using default service account: $SERVICE_ACCOUNT"
else
    echo "✅ Found service account: $SERVICE_ACCOUNT"
fi

# Grant Secret Manager access
echo "🔑 Granting Secret Manager access to service account..."
gcloud secrets add-iam-policy-binding GEMINI_API_KEY \
    --member="serviceAccount:$SERVICE_ACCOUNT" \
    --role="roles/secretmanager.secretAccessor"

echo "✅ Permissions granted"

# Update Cloud Run service to include the secret
echo "🚀 Updating Cloud Run service to include GEMINI_API_KEY secret..."
gcloud run services update chessdecoder \
    --region=europe-west1 \
    --update-secrets=GEMINI_API_KEY=GEMINI_API_KEY:latest

echo "✅ Cloud Run service updated!"
echo ""
echo "The service will automatically restart with the new secret configuration."
echo "You can verify by checking the service logs after a few moments."

