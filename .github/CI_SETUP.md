# CI/CD Setup Guide

## Overview
This repository uses GitHub Actions for continuous integration. The CI pipeline automatically runs on every pull request and push to main branches.

## Workflows

### 1. CI - Build and Test (`ci.yml`)
**Triggers:** Pull requests and pushes to `main`, `dev`, `dev-refactor`

**What it does:**
- ✅ Builds the .NET 9.0 solution
- ✅ Runs all 145 tests
- ✅ Generates code coverage reports
- ✅ Posts test results and coverage to PR comments
- ✅ Checks code formatting (non-blocking)
- ✅ Uploads test artifacts for debugging

**Permissions:**
- `contents: read` - Read repository code
- `checks: write` - Create check runs for test reports
- `pull-requests: write` - Post coverage comments on PRs

**Jobs:**
1. **build-and-test** - Main build and test execution
2. **code-style-check** - Format and style validation (non-blocking)
3. **ci-status** - Overall status aggregation

### 2. PR Agent (`pr-agent.yml`)
**Triggers:** PR creation, updates, and comments

**What it does:**
- AI-powered code review
- Suggests improvements
- Reviews for bugs and best practices

## Test Execution

Tests run automatically on:
- Every pull request
- Every push to protected branches
- Can be manually triggered from Actions tab

**Test Suite:**
- 144 passing tests
- 1 skipped test
- Covers repositories, services, controllers, and integration flows

## Code Coverage

Coverage reports are:
- Generated for each CI run
- Posted as PR comments
- Available as downloadable artifacts
- Stored for 30 days

## Branch Protection

### Recommended Settings
Configure these in **Settings → Branches → Branch protection rules**:

For `main` and `dev` branches:
- ✅ Require pull request reviews (1+ approvals)
- ✅ Require status checks to pass:
  - `CI Status Check`
  - `build-and-test`
- ✅ Require branches to be up to date
- ✅ Require conversation resolution
- ❌ Allow force pushes (disabled)
- ❌ Allow deletions (disabled)

## Local Development

### Run tests locally before pushing:
```bash
# Navigate to the API directory
cd ChessDecoderApi

# Restore dependencies
dotnet restore ChessDecoderApi.sln

# Build
dotnet build ChessDecoderApi.sln --configuration Release

# Run all tests
dotnet test ChessDecoderApi.sln --configuration Release --verbosity normal

# Run tests with coverage
dotnet test ChessDecoderApi.sln --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
```

### Check code formatting:
```bash
# Check if formatting is needed
dotnet format ChessDecoderApi.sln --verify-no-changes

# Apply formatting
dotnet format ChessDecoderApi.sln
```

## Troubleshooting

### Tests failing in CI but passing locally?
- Check .NET version matches (9.0.x)
- Ensure all dependencies are committed
- Check for environment-specific issues

### CI workflow not triggering?
- Ensure changes are in `ChessDecoderApi/**` directory
- Check branch names match the trigger configuration
- Verify workflow file syntax (YAML)

### Coverage reports not showing?
- Check that tests complete successfully
- Verify test results directory exists
- Review workflow logs for errors

## Monitoring

### View CI results:
1. Navigate to **Actions** tab
2. Select the relevant workflow run
3. View job details and logs
4. Download artifacts if needed

### Test results location:
- PR checks section (status badge)
- Actions → Workflow run → Test Results
- PR comments (coverage summary)
- Artifacts → test-results.trx

## Updating Workflows

To modify CI behavior:
1. Edit `.github/workflows/ci.yml`
2. Test changes on a feature branch
3. Merge after verification

**Note:** Workflow changes require repository write permissions.

## Performance

Current CI metrics:
- **Build time:** ~2-3 minutes
- **Test execution:** ~1-2 minutes  
- **Total CI time:** ~5 minutes
- **Timeout:** 10 minutes (configurable)

## Security

### Secrets used:
- `GITHUB_TOKEN` - Auto-provided by GitHub
- `OPENAI_API_KEY` - For PR Agent (optional)

No additional secrets required for basic CI.

## Support

For CI issues:
1. Check workflow logs in Actions tab
2. Review this documentation
3. Check GitHub Actions status page
4. Contact repository maintainers

## Future Enhancements

Potential improvements:
- [ ] Add deployment workflow for staging/production
- [ ] Implement Docker image building and scanning
- [ ] Add performance benchmarking
- [ ] Set up code quality metrics tracking
- [ ] Add automated dependency updates (Dependabot)

