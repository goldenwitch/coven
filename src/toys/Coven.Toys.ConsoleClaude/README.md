# Coven.Toys.ConsoleClaude

A minimal console chat application using Anthropic Claude.

## Prerequisites

- .NET 10 SDK
- Anthropic API key

## Configuration

Set environment variables or edit `Properties/launchSettings.json`:

| Variable | Description | Default |
|----------|-------------|---------|
| `ANTHROPIC_API_KEY` | Your Anthropic API key | (required) |
| `CLAUDE_MODEL` | Claude model to use | `claude-sonnet-4-20250514` |

## Running

```bash
# Using environment variables
export ANTHROPIC_API_KEY="sk-ant-your-key"
dotnet run

# Or using launchSettings.json (edit first, then)
dotnet run --launch-profile "Coven.Toys.ConsoleClaude"
```

## Features

- Console-based chat with Claude
- Declarative covenant routing
- Thought visibility (when using extended thinking models)
