#!/bin/bash

# Install Git Pre-commit Hook for AI PM Prioritization

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
GIT_HOOKS_DIR="$REPO_ROOT/.git/hooks"
PRE_COMMIT_HOOK_SOURCE="$SCRIPT_DIR/pre-commit-hook"
PRE_COMMIT_HOOK_TARGET="$GIT_HOOKS_DIR/pre-commit"

echo "Installing AI PM pre-commit hook..."

# Ensure .git/hooks directory exists
mkdir -p "$GIT_HOOKS_DIR"

# Remove existing pre-commit hook if it's not a symlink or if it's a broken symlink
if [ -f "$PRE_COMMIT_HOOK_TARGET" ] && [ ! -L "$PRE_COMMIT_HOOK_TARGET" ]; then
    echo "Existing pre-commit hook found. Backing it up to ${PRE_COMMIT_HOOK_TARGET}.bak"
    mv "$PRE_COMMIT_HOOK_TARGET" "${PRE_COMMIT_HOOK_TARGET}.bak"
elif [ -L "$PRE_COMMIT_HOOK_TARGET" ]; then
    echo "Existing pre-commit symlink found. Removing it."
    rm "$PRE_COMMIT_HOOK_TARGET"
fi

# Create a symlink to the pre-commit hook script
ln -s "$PRE_COMMIT_HOOK_SOURCE" "$PRE_COMMIT_HOOK_TARGET"

# Make the hook executable
chmod +x "$PRE_COMMIT_HOOK_SOURCE"
chmod +x "$PRE_COMMIT_HOOK_TARGET"

echo "✅ AI PM pre-commit hook installed successfully!"
echo "It will now automatically update issue priorities on commit if issue files are modified."

