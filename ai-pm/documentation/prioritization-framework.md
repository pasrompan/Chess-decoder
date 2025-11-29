# Prioritization Framework

This document explains how the AI PM system calculates priority scores for issues.

## Priority Formula

The priority score is calculated using the following formula:

```
Priority Score = (Impact × Impact Weight) / (Effort × Effort Weight) - (Dependency Penalty × Blocked Count)
```

### Components

#### Impact
- **Range**: 0-10
- **Description**: The business or technical impact of completing this issue
- **Scale**:
  - 0-2: Low impact (nice to have)
  - 3-5: Medium impact (improves system)
  - 6-8: High impact (significant value)
  - 9-10: Critical impact (blocking or essential)

#### Effort
- **Range**: 1-10
- **Description**: Estimated effort required to complete the issue
- **Scale**:
  - 1-2: Trivial (hours)
  - 3-4: Small (days)
  - 5-6: Medium (weeks)
  - 7-8: Large (months)
  - 9-10: Very large (multiple months)

#### Dependencies
- **Description**: Issues that must be completed before this one
- **Blocked Count**: Number of incomplete dependencies
- **Effect**: Each blocking dependency reduces priority score

### Weights and Penalties

Default configuration:

- **Impact Weight**: 1.0
- **Effort Weight**: 1.0
- **Dependency Penalty**: 0.5

These can be adjusted in `scripts/update-prioritization.sh`:

```bash
IMPACT_WEIGHT=1.0
EFFORT_WEIGHT=1.0
DEPENDENCY_PENALTY=0.5
```

## Calculation Examples

### Example 1: High Impact, Low Effort
- Impact: 8
- Effort: 2
- Blocked: 0

```
Priority = (8 × 1.0) / (2 × 1.0) - (0.5 × 0)
         = 8 / 2 - 0
         = 4.0
```

**Result**: High priority (4.0)

### Example 2: Medium Impact, Medium Effort
- Impact: 5
- Effort: 5
- Blocked: 0

```
Priority = (5 × 1.0) / (5 × 1.0) - (0.5 × 0)
         = 5 / 5 - 0
         = 1.0
```

**Result**: Medium priority (1.0)

### Example 3: High Impact, Blocked
- Impact: 9
- Effort: 3
- Blocked: 2 (has 2 incomplete dependencies)

```
Priority = (9 × 1.0) / (3 × 1.0) - (0.5 × 2)
         = 9 / 3 - 1.0
         = 3.0 - 1.0
         = 2.0
```

**Result**: Reduced priority due to dependencies (2.0)

### Example 4: Low Impact, High Effort
- Impact: 2
- Effort: 8
- Blocked: 0

```
Priority = (2 × 1.0) / (8 × 1.0) - (0.5 × 0)
         = 2 / 8 - 0
         = 0.25
```

**Result**: Low priority (0.25)

## Dependency Detection

The system automatically detects blocked issues:

1. **Parse Dependencies**: Reads the `dependencies` field from issue frontmatter
2. **Check Status**: Verifies if each dependency exists and is completed
3. **Count Blocked**: Counts incomplete dependencies
4. **Apply Penalty**: Reduces priority score proportionally

### Dependency Format

In issue frontmatter:

```yaml
dependencies: [ISSUE-001, ISSUE-002]
```

If ISSUE-001 is completed but ISSUE-002 is not, the blocked count is 1.

## Priority Score Interpretation

Priority scores are relative and should be compared across all active issues:

- **> 3.0**: Very high priority (quick wins or critical issues)
- **1.5 - 3.0**: High priority (valuable work)
- **0.5 - 1.5**: Medium priority (standard work)
- **< 0.5**: Low priority (consider deprioritizing)

## Automatic Updates

Priority scores are automatically recalculated:

- **On every commit**: Pre-commit hook runs prioritization
- **When dependencies change**: Blocked count updates
- **When impact/effort changes**: Scores recalculate

## Manual Updates

To manually update priorities:

```bash
./ai-pm/scripts/update-prioritization.sh
```

## Customization

### Adjusting Weights

Edit `scripts/update-prioritization.sh`:

```bash
# Increase impact importance
IMPACT_WEIGHT=1.5

# Decrease effort importance (makes high-effort issues more attractive)
EFFORT_WEIGHT=0.8

# Increase dependency penalty (more penalty for blocked issues)
DEPENDENCY_PENALTY=1.0
```

### Custom Formulas

You can modify the `calculate_priority()` function in the script to implement custom prioritization logic, such as:

- Time-based decay (older issues get priority boost)
- Label-based multipliers (bug fixes get higher priority)
- Team capacity considerations
- Business value multipliers

## Best Practices

1. **Be Honest with Estimates**: Accurate effort estimates lead to better prioritization
2. **Update Regularly**: Keep impact and effort current as requirements change
3. **Link Dependencies**: Always list blocking issues to get accurate priority
4. **Review Scores**: Periodically review if priorities match expectations
5. **Adjust Weights**: Tune weights based on project needs

## Output

The prioritization script generates:

1. **Updated Issue Files**: Priority scores in frontmatter
2. **Prioritized List**: `documentation/prioritized-issues.md` with sorted table

The prioritized list shows:
- Priority score
- Issue ID and title
- Impact, effort, and blocked count
- Jira link (if synced)
- Current status

