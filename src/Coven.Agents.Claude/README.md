# Coven.Agents.Claude

Anthropic Claude agent integration. Registers journals, gateway/session, transmuters, and optional streaming/windowing.

## Quick Start

```csharp
using Coven.Agents.Claude;

ClaudeClientConfig cfg = new()
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!,
    Model  = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-sonnet-4-20250514"
};

services.AddClaudeAgents(cfg, registration =>
{
    registration.EnableStreaming(); // optional
});
```

## Features
- Journals: `IScrivener<AgentEntry>`, `IScrivener<ClaudeEntry>` (keyed internal scrivener).
- Gateway: REST calls to Anthropic Messages API (`/v1/messages`) with optional SSE streaming.
- Transmuters: `ClaudeTransmuter` (Claude↔Agent), `ClaudeEntryToMessageTransmuter` (transcript), `ClaudeResponseOptionsTransmuter`.
- Windowing: default agent chunk/thought policies when streaming is enabled.
- Daemons: `ClaudeAgentDaemon` + windowing daemons (when streaming).
- Extended Thinking: support for Claude's extended thinking feature.

## Configuration

| Property | Required | Description |
|----------|----------|-------------|
| `ApiKey` | ✅ | Anthropic API key |
| `Model` | ✅ | Model identifier, e.g., `claude-sonnet-4-20250514` |
| `Endpoint` | | Custom API endpoint (defaults to `https://api.anthropic.com`) |
| `MaxTokens` | | Maximum tokens to generate (default 4096) |
| `Temperature` | | Sampling temperature (0.0 to 1.0) |
| `TopP` | | Nucleus sampling parameter |
| `TopK` | | Top-k sampling parameter |
| `SystemPrompt` | | System instruction prepended to conversations |
| `StopSequences` | | List of sequences that stop generation |
| `HistoryClip` | | Max transcript items to include (default unlimited) |
| `ExtendedThinking` | | Extended thinking configuration |

### Extended Thinking

Enable Claude's extended thinking feature for more complex reasoning:

```csharp
ClaudeClientConfig cfg = new()
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!,
    Model  = "claude-sonnet-4-20250514",
    ExtendedThinking = new ExtendedThinkingConfig
    {
        Enabled = true,
        BudgetTokens = 10000 // minimum 1024
    }
};
```

When enabled, thinking content streams as `ClaudeAfferentThinkingChunk` entries and is converted to `AgentAfferentThoughtChunk` for the agent journal.

## Override Policies (Streaming)

```csharp
using Coven.Core.Streaming;
using Coven.Agents;

// Response chunks: paragraphs OR soft cap
services.AddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
    new CompositeWindowPolicy<AgentAfferentChunk>(
        new AgentParagraphWindowPolicy(),
        new AgentMaxLengthWindowPolicy(4096)));

// Thought chunks: summary marker OR soft cap
services.AddScoped<IWindowPolicy<AgentAfferentThoughtChunk>>(_ =>
    new CompositeWindowPolicy<AgentAfferentThoughtChunk>(
        new AgentThoughtSummaryMarkerWindowPolicy(),
        new AgentThoughtMaxLengthWindowPolicy(4096)));
```

## Entry Types

| Entry Type | Direction | Description |
|------------|-----------|-------------|
| `ClaudeEfferent` | Outgoing | User message to Claude |
| `ClaudeAfferent` | Incoming | Complete response from Claude |
| `ClaudeAfferentChunk` | Incoming | Streaming text chunk (draft) |
| `ClaudeAfferentThinkingChunk` | Incoming | Streaming thinking chunk (draft) |
| `ClaudeThought` | Incoming | Complete thinking content |
| `ClaudeAck` | Internal | Synchronization acknowledgement |
| `ClaudeStreamCompleted` | Incoming | Marks end of streaming response |

## Notes

- Uses lightweight REST bindings with `HttpClient` to avoid external SDK dependencies.
- API key is sent via `x-api-key` header; uses `anthropic-version: 2023-06-01`.
- Thinking chunks are surfaced as thought entries where available (extended thinking models).
- Ensure outbound HTTPS is permitted to `api.anthropic.com`.

## See Also

- Branch: `Coven.Agents`.
- Architecture: Abstractions and Branches; Windowing and Shattering.
