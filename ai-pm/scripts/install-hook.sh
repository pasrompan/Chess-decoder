#!/bin/bash

# AI PM Git Hook Installer
# Installs the pre-commit hook to automatically update prioritization

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
HOOK_SOURCE="$SCRIPT_DIR/pre-commit-hook"
HOOK_TARGET="$REPO_ROOT/.git/hooks/pre-commit"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo "🔧 Installing AI PM git hook..."

# Check if we're in a git repository
if [ ! -d "$REPO_ROOT/.git" ]; then
    echo -e "${RED}❌ Error: Not a git repository${NC}"
    exit 1
fi

# Check if hook source exists
if [ ! -f "$HOOK_SOURCE" ]; then
    echo -e "${RED}❌ Error: Hook source not found: $HOOK_SOURCE${NC}"
    exit 1
fi

# Create hooks directory if it doesn't exist
mkdir -p "$(dirname "$HOOK_TARGET")"

# Check if pre-commit hook already exists
if [ -f "$HOOK_TARGET" ]; then
    # Check if it's already our hook
    if grep -q "AI PM Pre-commit Hook" "$HOOK_TARGET" 2>/dev/null; then
        echo -e "${YELLOW}⚠️  AI PM hook already installed${NC}"
        read -p "Replace existing hook? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            echo "Installation cancelled"
            exit 0
        fi
    else
        # Backup existing hook
        BACKUP="$HOOK_TARGET.backup.$(date +%Y%m%d_%H%M%S)"
        echo -e "${YELLOW}⚠️  Existing pre-commit hook found${NC}"
        echo "   Backing up to: $BACKUP"
        cp "$HOOK_TARGET" "$BACKUP"
        
        # Append our hook to existing hook
        echo "" >> "$HOOK_TARGET"
        echo "# AI PM Hook (appended)" >> "$HOOK_TARGET"
        cat "$HOOK_SOURCE" >> "$HOOK_TARGET"
        chmod +x "$HOOK_TARGET"
        
        echo -e "${GREEN}✅ Hook appended to existing pre-commit hook${NC}"
        echo "   Original hook backed up to: $BACKUP"
        exit 0
    fi
fi

# Install the hook
cp "$HOOK_SOURCE" "$HOOK_TARGET"
chmod +x "$HOOK_TARGET"

echo -e "${GREEN}✅ Git hook installed successfully!${NC}"
echo ""
echo "The hook will now:"
echo "  • Run prioritization updates on every commit"
echo "  • Only process when issues in ai-pm/issues/active/ are modified"
echo "  • Automatically stage updated priority scores"
echo ""
echo "To uninstall, run:"
echo "  rm $HOOK_TARGET"

