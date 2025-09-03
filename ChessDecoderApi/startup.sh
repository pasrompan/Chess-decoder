#!/bin/sh

# Create necessary directories with proper permissions
echo "[Startup] Creating application directories..."
mkdir -p /app/data /app/uploads /app/outputs

# Set proper permissions
chmod 755 /app/data /app/uploads /app/outputs

# Verify directories exist and are writable
echo "[Startup] Verifying directory permissions..."
ls -la /app/

if [ -d "/app/data" ]; then
    echo "[Startup] /app/data directory exists"
    touch /app/data/test_write && rm /app/data/test_write && echo "[Startup] /app/data is writable"
else
    echo "[Startup] ERROR: /app/data directory does not exist"
    exit 1
fi

# Start the application
echo "[Startup] Starting Chess Decoder API..."
exec dotnet ChessDecoderApi.dll
