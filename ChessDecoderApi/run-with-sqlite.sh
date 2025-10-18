#!/bin/bash
# Script to run the application in SQLite-only mode (for local testing)

echo "================================================"
echo "🗄️  Starting Chess Decoder API in SQLite Mode"
echo "================================================"
echo ""
echo "Note: This disables Firestore for local testing"
echo ""

# Temporarily rename Google credentials to force SQLite mode
CREDS_FILE="$HOME/.config/gcloud/application_default_credentials.json"
BACKUP_FILE="$HOME/.config/gcloud/application_default_credentials.json.backup"

if [ -f "$CREDS_FILE" ]; then
    echo "📝 Temporarily disabling Google Cloud credentials..."
    mv "$CREDS_FILE" "$BACKUP_FILE"
    echo "✅ Credentials backed up"
fi

echo "🚀 Starting application..."
echo ""

# Run the app
GOOGLE_CLOUD_PROJECT="" GOOGLE_APPLICATION_CREDENTIALS="" dotnet run

# Restore credentials when app exits
if [ -f "$BACKUP_FILE" ]; then
    echo ""
    echo "📝 Restoring Google Cloud credentials..."
    mv "$BACKUP_FILE" "$CREDS_FILE"
    echo "✅ Credentials restored"
fi

