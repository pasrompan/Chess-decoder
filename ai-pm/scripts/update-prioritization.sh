#!/bin/bash

# AI PM Prioritization Update Script
# Calculates priority scores for all active issues based on dependencies, effort, and impact

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ISSUES_DIR="$REPO_ROOT/ai-pm/issues/active"
DOCS_DIR="$REPO_ROOT/ai-pm/documentation"

# Configuration (matching prioritization-framework.md)
IMPACT_WEIGHT=1.0
EFFORT_WEIGHT=1.0
DEPENDENCY_PENALTY=0.5
MIN_EFFORT=1  # Avoid division by zero

# Check for 'bc' utility
if ! command -v bc &> /dev/null; then
    echo "Error: 'bc' command not found. Please install 'bc' (e.g., 'sudo apt-get install bc' or 'brew install bc')."
    exit 1
fi

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "🔄 Updating issue prioritization..."

# Check if issues directory exists
if [ ! -d "$ISSUES_DIR" ]; then
    echo "⚠️  Issues directory not found: $ISSUES_DIR"
    exit 0
fi

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
    local new_value="$3"
    
    # Escape new_value for sed
    local escaped_new_value=$(echo "$new_value" | sed -e 's/[\/&]/\\&/g')

    # Use awk to update the value within the YAML frontmatter
    awk -v key="$key" -v new_value="$escaped_new_value" '
        /^---$/ { in_frontmatter=!in_frontmatter; print; next }
        in_frontmatter && $1 == key":" {
            print key ": " new_value
            found=1
            next
        }
        { print }
        END {
            if (!found && in_frontmatter) {
                # If key not found in frontmatter, add it before the closing ---
                # This case should ideally not happen if template is used, but as a fallback
                print key ": " new_value
            }
        }
    ' "$file" > "${file}.tmp" && mv "${file}.tmp" "$file"
}

# Function to calculate priority score
calculate_priority() {
    local impact="$1"
    local effort="$2"
    local blocked_count="$3"
    
    # Ensure effort is at least MIN_EFFORT
    if (( $(echo "$effort < $MIN_EFFORT" | bc -l) )); then
        effort=$MIN_EFFORT
    fi
    
    # Calculate: (impact * impact_weight) / (effort * effort_weight) - (dependency_penalty * blocked_count)
    local numerator=$(echo "scale=4; $impact * $IMPACT_WEIGHT" | bc -l)
    local denominator=$(echo "scale=4; $effort * $EFFORT_WEIGHT" | bc -l)
    local ratio=$(echo "scale=4; $numerator / $denominator" | bc -l)
    local penalty=$(echo "scale=4; $DEPENDENCY_PENALTY * $blocked_count" | bc -l)
    local priority=$(echo "scale=4; $ratio - $penalty" | bc -l)
    
    # Ensure priority is not negative
    if (( $(echo "$priority < 0" | bc -l) )); then
        priority=0
    fi
    
    echo "$priority"
}

# Function to count blocked dependencies
count_blocked_dependencies() {
    local file="$1"
    local dependencies_str=$(extract_frontmatter "$file" "dependencies")
    
    if [ -z "$dependencies_str" ] || [ "$dependencies_str" = "[]" ]; then
        echo "0"
        return
    fi
    
    # Remove brackets and split by comma
    local deps=$(echo "$dependencies_str" | sed 's/\[//;s/\]//;s/,/\n/g' | tr -d ' ' | grep -v '^$')
    
    local count=0
    while IFS= read -r dep_id; do
        if [ -n "$dep_id" ]; then
            # Find dependency file (handle spaces in filenames)
            local dep_file=$(find "$ISSUES_DIR" -name "${dep_id}.md" -o -name "${dep_id} - *.md" 2>/dev/null | head -1)
            if [ -n "$dep_file" ] && [ -f "$dep_file" ]; then
                local dep_status=$(extract_frontmatter "$dep_file" "status")
                if [ "$dep_status" != "completed" ]; then
                    count=$((count + 1))
                fi
            else
                # Dependency file not found, assume it's blocking
                count=$((count + 1))
            fi
        fi
    done <<< "$deps"
    
    echo "$count"
}

# Array to store issue data for sorting
declare -a issue_data

# Process each issue file
issue_count=0
for issue_file in "$ISSUES_DIR"/*.md; do
    [ -f "$issue_file" ] || continue
    
    issue_count=$((issue_count + 1))
    issue_id=$(basename "$issue_file" .md)
    
    # Extract current values
    impact=$(extract_frontmatter "$issue_file" "impact")
    effort=$(extract_frontmatter "$issue_file" "effort")
    status=$(extract_frontmatter "$issue_file" "status")
    
    # Default to 0 if empty or non-numeric
    impact=${impact:-0}
    effort=${effort:-0}

    # Skip if not active
    if [ "$status" != "active" ]; then
        continue
    fi
    
    # Count blocked dependencies
    blocked_count=$(count_blocked_dependencies "$issue_file")
    
    # Calculate new priority
    new_priority=$(calculate_priority "$impact" "$effort" "$blocked_count")
    
    # Update priority in file
    update_frontmatter "$issue_file" "priority_score" "$new_priority"
    update_frontmatter "$issue_file" "updated_date" "$(date +"%Y-%m-%d")"
    
    echo "  ✓ Updated $issue_id: priority=$new_priority (impact=$impact, effort=$effort, blocked=$blocked_count)"
    
    # Store for sorting
    issue_data+=("$new_priority|$issue_id|$impact|$effort|$blocked_count")
done

if [ $issue_count -eq 0 ]; then
    echo "ℹ️  No active issues found to prioritize."
    exit 0
fi

# Sort issues by priority (descending) and generate prioritized list
echo ""
echo "📊 Generating prioritized issues list..."

# Sort by priority (first field, descending)
IFS=$'\n' sorted_issues=($(printf '%s\n' "${issue_data[@]}" | sort -t'|' -k1 -rn))

# Generate prioritized issues markdown
{
    cat << EOF
# Prioritized Issues

This file is automatically generated by the prioritization framework. It is updated on every commit.

**Last Updated**: $(date +"%Y-%m-%d %H:%M:%S")

## Priority Calculation

Priority Score = (Impact × Impact Weight) / (Effort × Effort Weight) - (Dependency Penalty × Blocked Count)

- **Impact Weight**: $IMPACT_WEIGHT
- **Effort Weight**: $EFFORT_WEIGHT
- **Dependency Penalty**: $DEPENDENCY_PENALTY

## Active Issues (Sorted by Priority)

| Priority | Issue ID | Impact | Effort | Blocked | Status |
|----------|----------|--------|--------|---------|--------|
EOF

    for issue_info in "${sorted_issues[@]}"; do
        IFS='|' read -r priority issue_id impact effort blocked_count <<< "$issue_info"
        issue_file=$(find "$ISSUES_DIR" -name "${issue_id}.md" 2>/dev/null | head -1)
        
        if [ -n "$issue_file" ] && [ -f "$issue_file" ]; then
            title=$(grep -m 1 "^# " "$issue_file" | sed 's/^# //' || echo "No title")
            status=$(extract_frontmatter "$issue_file" "status")
            
            # Create link to issue file (handle spaces in filename)
            issue_link="[${issue_id}](issues/active/${issue_id}.md)"
            
            printf "| %.4f | %s - %s | %s | %s | %s | %s |\n" "$priority" "$issue_link" "$title" "$impact" "$effort" "$blocked_count" "$status"
        fi
    done
} > "$DOCS_DIR/prioritized-issues.md"

echo ""
echo -e "${GREEN}✅ Prioritization update complete!${NC}"
echo "   Processed $issue_count issue(s)"
echo "   Updated: $DOCS_DIR/prioritized-issues.md"

