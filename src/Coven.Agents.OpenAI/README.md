# Coven.Agents.OpenAI

OpenAI agent integration (official .NET SDK). Registers journals, gateway/session, transmuters, and optional streaming/windowing.

## What’s Inside

- Config: `OpenAIClientConfig` (API key, model, optional org/project; reasoning options).
- Registration: `AddOpenAIAgents(config, registration => ...)`.
- Gateways: request vs streaming connections.
- Journals: `IScrivener<AgentEntry>`, `IScrivener<OpenAIEntry>`.
- Transmuters: `OpenAITransmuter` (OpenAI↔Agent), `OpenAIEntryToResponseItemTransmuter` (templating), `OpenAIResponseOptionsTransmuter`.
- Windowing: default policies for response chunks and thought chunks when streaming is enabled.
- Daemons: `OpenAIAgentDaemon` and windowing daemons (when streaming).

## Quick Start

```csharp
using Coven.Agents.OpenAI;

OpenAIClientConfig cfg = new()
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    Model  = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5-2025-08-07",
};

services.AddOpenAIAgents(cfg, registration =>
{
    registration.EnableStreaming(); // optional, enables windowing daemons
});
```

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

## Templating (Optional)

Provide an `ITransmuter<OpenAIEntry, ResponseItem?>` to inject context (usernames, system preamble, etc.). See sample `DiscordOpenAITemplatingTransmuter` in `src/samples/01.DiscordAgent`.

## Requirements

- Valid OpenAI API key and model name for your account.
- Network egress to OpenAI endpoints.

## See Also

- Branch: `Coven.Agents`.
- Architecture: Abstractions and Branches; Windowing and Shattering.
