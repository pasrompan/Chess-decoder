#!/bin/bash

# Script to build and run the evaluation runner
# Make sure the API is running on http://localhost:5100

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
EVALUATION_EXAMPLES_PATH="$PROJECT_DIR/Tests/data/EvaluationExamples"

echo "Building EvaluationRunner..."
cd "$SCRIPT_DIR"
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo ""
echo "Running evaluation..."
echo "Make sure the API is running on http://localhost:5100"
echo ""

dotnet run -c Release -- "$EVALUATION_EXAMPLES_PATH"

