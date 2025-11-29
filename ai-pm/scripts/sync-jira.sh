#!/bin/bash

# AI PM Jira Sync Script
# Syncs issues between local markdown files and Jira (Project PG, Epic ChessScriber)
# Uses Atlassian MCP server for Jira operations

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ISSUES_DIR="$REPO_ROOT/ai-pm/issues/active"

# Jira Configuration
JIRA_PROJECT="PG"
JIRA_EPIC="ChessScriber"
JIRA_CLOUD_ID=""  # Will be fetched from MCP

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "🔄 Syncing issues with Jira..."

# Function to extract YAML frontmatter value
extract_frontmatter() {
    local file="$1"
    local key="$2"
    awk -v key="$key" '
        /^---$/ { in_frontmatter=!in_frontmatter; next }
        in_frontmatter && $1 == key":" {
            gsub(/^[^:]*:[[:space:]]*/, "")
            gsub(/^["'\'']|["'\'']$/, "")
            print
            exit
        }
    ' "$file"
}

# Function to update YAML frontmatter value
update_frontmatter() {
    local file="$1"
    local key="$2"
    local value="$3"
    
    # Create temp file
    local temp_file=$(mktemp)
    
    # Process file
    awk -v key="$key" -v value="$value" '
        BEGIN { updated=0 }
        /^---$/ { 
            in_frontmatter=!in_frontmatter
            print
            next
        }
        in_frontmatter && $1 == key":" {
            printf "%s: %s\n", key, value
            updated=1
            next
        }
        { print }
        END {
            if (!updated && in_frontmatter) {
                printf "%s: %s\n", key, value
            }
        }
    ' "$file" > "$temp_file"
    
    mv "$temp_file" "$file"
}

# Function to get cloud ID from Atlassian
get_cloud_id() {
    # This would typically call the MCP server
    # For now, we'll use a placeholder that needs to be configured
    if [ -z "$JIRA_CLOUD_ID" ]; then
        echo "⚠️  Jira Cloud ID not configured. Please set JIRA_CLOUD_ID environment variable or update script."
        echo "   You can get it from: https://paschalis-rompanos.atlassian.net/"
        return 1
    fi
    echo "$JIRA_CLOUD_ID"
}

# Function to create Jira issue from markdown
create_jira_issue() {
    local issue_file="$1"
    local issue_id=$(basename "$issue_file" .md)
    local title=$(grep -m 1 "^# " "$issue_file" | sed 's/^# //' || echo "No title")
    local description=$(sed -n '/^## Description$/,/^## /p' "$issue_file" | sed '1d;$d' || echo "")
    local impact=$(extract_frontmatter "$issue_file" "impact")
    local effort=$(extract_frontmatter "$issue_file" "effort")
    
    echo "  📝 Creating Jira issue for $issue_id..."
    echo "     Title: $title"
    echo "     Impact: $impact, Effort: $effort"
    
    # Note: Actual Jira creation would use MCP server
    # This is a placeholder for the integration
    echo "     ⚠️  Jira creation requires MCP server integration"
    echo "     To create issue, use: mcp_Atlassian-MCP-Server_createJiraIssue"
    echo "     Project: $JIRA_PROJECT, Epic: $JIRA_EPIC"
}

# Function to update Jira issue from markdown
update_jira_issue() {
    local issue_file="$1"
    local jira_key=$(extract_frontmatter "$issue_file" "jira_key")
    
    if [ -z "$jira_key" ] || [ "$jira_key" = '""' ]; then
        return 0
    fi
    
    local title=$(grep -m 1 "^# " "$issue_file" | sed 's/^# //' || echo "")
    local description=$(sed -n '/^## Description$/,/^## /p' "$issue_file" | sed '1d;$d' || echo "")
    local status=$(extract_frontmatter "$issue_file" "status")
    
    echo "  🔄 Updating Jira issue $jira_key..."
    echo "     Status: $status"
    
    # Note: Actual Jira update would use MCP server
    echo "     ⚠️  Jira update requires MCP server integration"
    echo "     To update issue, use: mcp_Atlassian-MCP-Server_editJiraIssue"
}

# Function to sync from Jira to markdown
sync_from_jira() {
    local jira_key="$1"
    local issue_file="$ISSUES_DIR/${jira_key}.md"
    
    echo "  📥 Syncing $jira_key from Jira..."
    
    # Note: Actual Jira fetch would use MCP server
    # This is a placeholder for the integration
    echo "     ⚠️  Jira fetch requires MCP server integration"
    echo "     To fetch issue, use: mcp_Atlassian-MCP-Server_getJiraIssue"
    echo "     Cloud ID: $(get_cloud_id 2>/dev/null || echo 'not configured')"
}

# Main sync logic
sync_mode="${1:-both}"

case "$sync_mode" in
    "to-jira")
        echo "📤 Syncing local issues to Jira..."
        for issue_file in "$ISSUES_DIR"/*.md; do
            [ -f "$issue_file" ] || continue
            
            issue_id=$(basename "$issue_file" .md)
            jira_key=$(extract_frontmatter "$issue_file" "jira_key")
            
            if [ -z "$jira_key" ] || [ "$jira_key" = '""' ]; then
                # Create new Jira issue
                create_jira_issue "$issue_file"
            else
                # Update existing Jira issue
                update_jira_issue "$issue_file"
            fi
        done
        ;;
    "from-jira")
        echo "📥 Syncing Jira issues to local files..."
        echo "  ⚠️  This requires JQL query to fetch issues"
        echo "  Use: mcp_Atlassian-MCP-Server_searchJiraIssuesUsingJql"
        echo "  Query: project = $JIRA_PROJECT AND \"Epic Link\" = $JIRA_EPIC"
        ;;
    "both"|*)
        echo "🔄 Syncing both directions..."
        echo "  This is a two-way sync operation"
        echo ""
        echo "  📤 To Jira:"
        sync_mode="to-jira" "$0" to-jira
        echo ""
        echo "  📥 From Jira:"
        sync_mode="from-jira" "$0" from-jira
        ;;
esac

echo ""
echo -e "${GREEN}✅ Jira sync complete!${NC}"
echo ""
echo "ℹ️  Note: This script provides the framework for Jira integration."
echo "   Actual Jira operations require the Atlassian MCP server to be"
echo "   configured and accessible. Use the MCP tools to perform the actual"
echo "   create/update/fetch operations."
echo ""
echo "   Configuration:"
echo "   - Project: $JIRA_PROJECT"
echo "   - Epic: $JIRA_EPIC"
echo "   - Cloud ID: $(get_cloud_id 2>/dev/null || echo 'not configured - set JIRA_CLOUD_ID')"

