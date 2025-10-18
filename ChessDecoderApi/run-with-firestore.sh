#!/bin/bash
# Script to run the application in Firestore mode (production-like)

echo "================================================"
echo "☁️  Starting Chess Decoder API in Firestore Mode"
echo "================================================"
echo ""
echo "Note: This uses Google Cloud Firestore"
echo ""

# Restore .env if it was modified
if [ -f ".env.backup" ]; then
    echo "📝 Restoring original .env file..."
    cp .env.backup .env
    echo "✅ .env restored"
fi

echo "🚀 Starting application..."
echo ""

# Run the app normally
dotnet run

