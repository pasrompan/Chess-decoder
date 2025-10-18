# Automatic PR Review Setup

This repository is configured with AI-powered automatic PR reviews using PR-Agent.

## Setup Instructions

### 1. Add OpenAI API Key Secret

You need to add your OpenAI API key as a GitHub secret:

1. Go to your repository on GitHub
2. Navigate to: **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add the following secret:
   - **Name**: `OPENAI_API_KEY`
   - **Value**: Your OpenAI API key from https://platform.openai.com/api-keys

### 2. How It Works

Once the secret is added, the workflow will automatically:

- **Review PRs**: When a PR is opened, reopened, or updated
- **Provide Feedback**: Inline comments on specific code changes
- **Security Analysis**: Flag potential security issues
- **Code Suggestions**: Suggest improvements
- **Effort Estimation**: Estimate review effort
- **Test Coverage**: Check if tests are adequate

### 3. Using PR-Agent Commands

You can interact with PR-Agent by commenting on PRs with special commands:

- `/review` - Trigger a full review
- `/describe` - Auto-generate/update PR description
- `/improve` - Get code improvement suggestions
- `/ask <question>` - Ask questions about the PR
- `/update_changelog` - Update changelog based on PR

### 4. Configuration

The behavior is controlled by `.pr_agent.toml` in the repository root. Current settings:

- Uses GPT-4o model for best results
- Provides up to 4 code suggestions per review
- Includes security review
- Provides effort estimation
- Enables inline comments

### 5. Cost Considerations

PR-Agent uses your OpenAI API key, so reviews will incur costs based on OpenAI's pricing:
- GPT-4o: ~$0.01-0.05 per PR review (depending on PR size)
- Reviews run automatically on PR open/update

### 6. Troubleshooting

If reviews aren't appearing:
1. Check that `OPENAI_API_KEY` secret is set correctly
2. Verify the workflow is enabled: **Actions** tab → **PR Agent**
3. Check workflow runs for errors
4. Ensure PRs are not in draft mode (reviews skip draft PRs)

### 7. Disable Automatic Reviews

To disable automatic reviews but keep manual commands:

Edit `.pr_agent.toml`:
```toml
[pr_reviewer]
automatic_review = false
```

Then trigger reviews manually with `/review` comments.

## Learn More

- PR-Agent GitHub: https://github.com/Codium-ai/pr-agent
- Full documentation: https://pr-agent-docs.codium.ai/

