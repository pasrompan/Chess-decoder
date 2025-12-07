# Prioritization Framework

This document explains how the AI PM system calculates priority scores for issues. The prioritization system helps you focus on the most valuable work by automatically ranking issues based on impact, effort, and dependencies.

## Priority Formula

The priority score is calculated using the following formula:

```
Priority Score = (Impact × Impact Weight) / (Effort × Effort Weight) - (Dependency Penalty × Blocked Count)
```

### Formula Explanation

- **Higher scores = Higher priority**: Issues with higher priority scores should be tackled first
- **Impact/Effort ratio**: The core of the formula rewards high-impact, low-effort work (quick wins)
- **Dependency penalty**: Reduces priority for issues that are blocked by incomplete dependencies
- **Minimum score**: Priority scores cannot go below 0

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

These can be adjusted in the prioritization script configuration section:

```bash
IMPACT_WEIGHT=1.0
EFFORT_WEIGHT=1.0
DEPENDENCY_PENALTY=0.5
```

The weights control how much each factor influences the final priority score. Higher impact weight means impact has more influence, lower effort weight means effort has less negative impact.

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

Priority scores are relative and should be compared across all active issues. The scores help you identify:

- **> 3.0**: Very high priority - Quick wins or critical issues. These should be tackled first.
- **1.5 - 3.0**: High priority - Valuable work that delivers good return on investment.
- **0.5 - 1.5**: Medium priority - Standard work items with balanced impact and effort.
- **< 0.5**: Low priority - Consider deprioritizing or breaking into smaller pieces.

### Understanding the Scores

The priority score represents the **value-to-effort ratio** adjusted for dependencies:

- A score of 4.0 means the issue delivers 4 units of value per unit of effort
- A score of 0.5 means the issue delivers 0.5 units of value per unit of effort
- Blocked issues have their scores reduced, encouraging you to complete dependencies first

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

This will:
1. Scan all issues in `ai-pm/issues/active/`
2. Calculate priority scores based on current impact, effort, and dependencies
3. Update the `priority_score` field in each issue's frontmatter
4. Generate `ai-pm/documentation/prioritized-issues.md` with the sorted list

## Customization

### Adjusting Weights

Edit the prioritization script configuration:

```bash
# Increase impact importance (prioritize high-impact work)
IMPACT_WEIGHT=1.5

# Decrease effort importance (makes high-effort issues more attractive)
EFFORT_WEIGHT=0.8

# Increase dependency penalty (more penalty for blocked issues)
DEPENDENCY_PENALTY=1.0
```

### Alternative Prioritization Strategies

You can modify the prioritization formula to implement different strategies based on your project's current needs:

#### Value-Based Prioritization
Focus on maximizing value delivered regardless of effort:
```bash
# Higher impact weight, lower effort weight
IMPACT_WEIGHT=2.0
EFFORT_WEIGHT=0.5
```
**Use when**: You want to focus on high-value work, even if it takes longer.

#### Quick Wins Strategy
Prioritize low-effort, high-impact work to build momentum:
```bash
# Very high impact weight, very low effort weight
IMPACT_WEIGHT=3.0
EFFORT_WEIGHT=0.3
```
**Use when**: You need to show progress quickly or build team momentum.

#### Risk Mitigation
Prioritize issues that unblock others to reduce project risk:
```bash
# High dependency penalty to avoid blocked work
DEPENDENCY_PENALTY=2.0
```
**Use when**: You have many interdependent issues and want to avoid bottlenecks.

#### Balanced Approach (Default)
Equal weight to impact and effort:
```bash
IMPACT_WEIGHT=1.0
EFFORT_WEIGHT=1.0
DEPENDENCY_PENALTY=0.5
```
**Use when**: You want a balanced view that considers both value and effort.

### Custom Formulas

You can extend the prioritization script to include additional factors:

- **Time-based decay**: Older issues get a small priority boost
- **Category multipliers**: Different weights for bugs vs features
- **Urgency flags**: Manual override for critical issues
- **Team capacity**: Adjust based on available resources

## Best Practices

1. **Be Honest with Estimates**: Accurate effort estimates lead to better prioritization. Overestimating effort can hide valuable work.

2. **Update Regularly**: Keep impact and effort current as requirements change. Re-evaluate when:
   - Requirements become clearer
   - Technical complexity is better understood
   - Business priorities shift

3. **Link Dependencies**: Always list blocking issues to get accurate priority. The system will automatically reduce priority for blocked work.

4. **Review Scores**: Periodically review if priorities match expectations. If scores don't align with your intuition:
   - Check if impact/effort values are accurate
   - Consider adjusting weights
   - Verify dependencies are correctly listed

5. **Adjust Weights**: Tune weights based on project needs and team preferences. Different projects may benefit from different strategies.

6. **Break Down Large Issues**: Very high effort (8-10) issues might benefit from being split into smaller, more manageable pieces.

7. **Use Dependencies Strategically**: Link related issues to ensure logical work sequencing, not just technical dependencies.

## Output

The prioritization script generates:

1. **Updated Issue Files**: Priority scores are written to the `priority_score` field in each issue's frontmatter
2. **Prioritized List**: `documentation/prioritized-issues.md` with a sorted table of all active issues

The prioritized list shows:
- **Priority score**: The calculated priority (higher = more important)
- **Issue ID and title**: Clickable link to the issue file
- **Impact**: The impact value (0-10)
- **Effort**: The effort estimate (1-10)
- **Blocked**: Number of incomplete dependencies
- **Status**: Current issue status

This provides a clear, actionable view of which issues should be tackled first based on the calculated priorities. The list is automatically sorted with highest priority issues at the top.

## Workflow Integration

The prioritization system integrates seamlessly with your git workflow:

1. **Create or update issues** in `ai-pm/issues/active/`
2. **Set impact and effort** values in the frontmatter
3. **List dependencies** if the issue depends on others
4. **Commit changes** - the git hook automatically recalculates priorities
5. **Review** `prioritized-issues.md` to see the updated ranking
6. **Work on high-priority issues** first for maximum value delivery

