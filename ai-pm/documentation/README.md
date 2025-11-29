# AI PM Issue Tracking System

This directory contains the AI Product Manager (PM) issue tracking system for the Chess-decoder project. The system provides automated issue prioritization, Jira integration, and dynamic updates on every commit.

## Overview

The AI PM system manages project issues using markdown files with structured metadata. It automatically calculates priority scores based on dependencies, effort estimation, and impact, and can sync with Jira for centralized project management.

## Features

- **Markdown-based Issue Tracking**: Human-readable issue files with structured metadata
- **Dynamic Prioritization**: Automatic priority calculation on every commit
- **Jira Integration**: Bidirectional sync with Jira project PG (Epic ChessScriber)
- **Dependency Tracking**: Automatic detection of blocked issues
- **Archive System**: Organized storage for completed and deprioritized issues

## Folder Structure

```
ai-pm/
├── issues/
│   ├── active/          # Currently active issues
│   ├── archive/
│   │   ├── completed/   # Finished issues
│   │   └── deprioritized/ # Deprioritized issues
│   └── templates/       # Issue templates
├── documentation/       # This documentation
└── scripts/             # Automation scripts
```

## Quick Start

### 1. Create a New Issue

Copy the template and create a new issue:

```bash
cp ai-pm/issues/templates/issue-template.md ai-pm/issues/active/ISSUE-001.md
```

Edit the file and fill in:
- Issue title and description
- Impact level (0-10)
- Effort estimation (1-10)
- Dependencies (list of issue IDs)
- Acceptance criteria

### 2. Install Git Hook

Install the pre-commit hook to automatically update priorities:

```bash
./ai-pm/scripts/install-hook.sh
```

### 3. View Prioritized Issues

After committing, view the prioritized list:

```bash
cat ai-pm/documentation/prioritized-issues.md
```

### 4. Sync with Jira

Sync issues with Jira (requires MCP server):

```bash
./ai-pm/scripts/sync-jira.sh
```

## Issue Format

Each issue is a markdown file with YAML frontmatter:

```yaml
---
id: ISSUE-001
status: active
priority_score: 2.5
effort: 3
impact: 8
dependencies: []
jira_key: "PG-123"
jira_url: "https://paschalis-rompanos.atlassian.net/browse/PG-123"
created_date: "2025-01-20"
updated_date: "2025-01-20"
---
```

## Priority Calculation

Priority scores are calculated automatically using:

```
Priority = (Impact × Impact Weight) / (Effort × Effort Weight) - (Dependency Penalty × Blocked Count)
```

See [Prioritization Framework](./prioritization-framework.md) for details.

## Status Values

- **active**: Issue is currently being worked on or planned
- **completed**: Issue is finished (moved to archive/completed/)
- **deprioritized**: Issue is no longer a priority (moved to archive/deprioritized/)

## Commands

### Update Prioritization Manually

```bash
./ai-pm/scripts/update-prioritization.sh
```

### Sync with Jira

```bash
# Sync both directions
./ai-pm/scripts/sync-jira.sh

# Sync to Jira only
./ai-pm/scripts/sync-jira.sh to-jira

# Sync from Jira only
./ai-pm/scripts/sync-jira.sh from-jira
```

## Documentation

- [Prioritization Framework](./prioritization-framework.md) - How priority scores are calculated
- [Jira Integration](./jira-integration.md) - How to sync with Jira

## Workflow

1. **Create Issue**: Create a new markdown file in `issues/active/`
2. **Set Metadata**: Fill in impact, effort, and dependencies
3. **Commit**: Git hook automatically updates priority scores
4. **Review**: Check `documentation/prioritized-issues.md` for sorted list
5. **Work on Issue**: Update status and progress
6. **Complete**: Move to `archive/completed/` when done

## Integration with Jira

The system integrates with Jira project **PG** under Epic **ChessScriber**. See [Jira Integration Guide](./jira-integration.md) for setup and usage.

## Troubleshooting

### Priority scores not updating

- Ensure the git hook is installed: `./ai-pm/scripts/install-hook.sh`
- Check that issues have valid impact and effort values
- Verify the script is executable: `chmod +x ai-pm/scripts/update-prioritization.sh`

### Jira sync not working

- Verify MCP server is configured
- Check Jira Cloud ID is set
- Ensure you have access to project PG

## Contributing

When adding new issues:
1. Use the template from `issues/templates/`
2. Follow the naming convention: `ISSUE-XXX.md`
3. Fill in all required metadata
4. Set realistic impact and effort estimates
5. Link dependencies if applicable

