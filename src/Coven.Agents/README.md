# Coven.Agents

Branch abstraction for AI agents. Defines typed agent entries and batch transmutation for streamed responses and thoughts.

## What’s Inside

- Entries: `AgentPrompt`, `AgentResponse`, `AgentThought`, `AgentAck`.
- Streaming entries: `AgentAfferentChunk`, `AgentEfferentChunk`, `AgentAfferentThoughtChunk`, `AgentEfferentThoughtChunk`, `AgentStreamCompleted`.
- Batch transmuters: `AgentAfferentBatchTransmuter` (response chunks → `AgentResponse`), `AgentAfferentThoughtBatchTransmuter` (thought chunks → `AgentThought`).

## Why use it?

- Decouple agent‑facing logic from specific providers (OpenAI, etc.).
- Stream agent output and surface user‑visible responses with semantic windowing.

## Usage (Conceptual)

Applications typically integrate a concrete leaf (e.g., `Coven.Agents.OpenAI`) which registers journals, daemons, and default policies. Your blocks read/write `AgentEntry`:

```csharp
await _agents.WriteAsync(new AgentPrompt("user", "hello"), ct);

await foreach ((long _, AgentEntry? entry) in _agents.TailAsync(0, ct))
{
    if (entry is AgentResponse r)
    {
        // forward to chat
    }
}
```

## See Also

- Provider: `Coven.Agents.OpenAI`.
- Architecture: Abstractions and Branches; Windowing and Shattering.
