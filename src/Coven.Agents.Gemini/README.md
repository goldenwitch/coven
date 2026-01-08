# Coven.Agents.Gemini

Google Gemini agent integration (REST) that mirrors the OpenAI leaf: registers journals, gateway/session, transmuters, and optional streaming/windowing.

## Quick Start

```csharp
using Coven.Agents.Gemini;

GeminiClientConfig cfg = new()
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")!,
    Model  = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-3.0-pro"
};

services.AddGeminiAgents(cfg, registration =>
{
    registration.EnableStreaming(); // optional
});
```

## Features
- Journals: `IScrivener<AgentEntry>`, `IScrivener<GeminiEntry>` (keyed internal scrivener).
- Gateway: REST calls to `generateContent` or `streamGenerateContent`.
- Transmuters: `GeminiTransmuter` (Gemini↔Agent), `GeminiEntryToContentTransmuter` (transcript), `GeminiResponseOptionsTransmuter`.
- Windowing: default agent chunk/thought policies when streaming is enabled.
- Daemons: `GeminiAgentDaemon` + windowing daemons (when streaming).

## Configuration
- `ApiKey` (required): Gemini API key.
- `Model` (required): e.g., `gemini-3.0-pro` or `gemini-2.0-flash`.
- `Temperature`, `TopP`, `TopK`, `MaxOutputTokens` (optional generation config).
- `HistoryClip` (optional): max transcript items to include (default unlimited).
- `SystemInstruction` (optional): system prompt injected as instruction.
- `ResponseMimeType` (optional): request JSON style responses.
- `SafetySettings` (optional): list of category + threshold pairs.

## Notes
- Uses lightweight REST bindings to avoid external SDK dependencies; headers carry the API key.
- Reasoning/safety deltas are surfaced as thought chunks where available; blocked prompts emit a safety entry.
- Ensure outbound HTTPS is permitted to `generativelanguage.googleapis.com`.
