#!/bin/bash

# Script to import Jira issues from Epic ChessScriber and convert to markdown
# This script uses the MCP server results that were fetched

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ISSUES_DIR="$REPO_ROOT/ai-pm/issues"

echo "📥 Importing Jira issues from Epic ChessScriber..."

# This script will be called with issue data
# For now, we'll process the issues we found

echo "✅ Import script ready. Use MCP tools to fetch issues and convert them."

