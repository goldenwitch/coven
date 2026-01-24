# Coven.Covenants

Covenant builder and validation for compile-time verifiable journal protocols.

## What is a Covenant?

A **Covenant** is a compile-time connectivity guarantee for a journal protocol. When you define a Covenant, the system proves:

- Every entry type has a consumer (no dead letters)
- Every consumer has a producer (no orphans)
- The graph is fully connected (no islands)

## Usage

```csharp
// 1. Define the covenant
public sealed class ChatCovenant : ICovenant
{
    public static string Name => "Chat";
}

// 2. Mark entry types with covenant membership
public record UserMessage(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>;

public record ChatChunk(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>;

public record ChatEfferent(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>;

public record AssistantMessage(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSink<ChatCovenant>;

// 3. Wire via the builder
services.AddCovenant<ChatCovenant>(covenant =>
{
    covenant.Source<UserMessage>();
    covenant.Source<ChatChunk>();
    covenant.Sink<AssistantMessage>();
    
    covenant.Window<ChatChunk, ChatEfferent>(
        policy: new ParagraphWindowPolicy<ChatChunk>(),
        transmuter: new ChatChunkBatchTransmuter());
    
    covenant.Transform<ChatEfferent, AssistantMessage>(
        transmuter: new ChatEfferentToMessageTransmuter());
});
```

## Packages

| Package | Purpose |
|---------|---------|
| `Coven.Core` | Marker interfaces (`ICovenant`, `ICovenantEntry<T>`, etc.) |
| `Coven.Covenants` | Builder, validator, DI extensions |

## What's Excluded from Covenants?

The covenant validation only covers types that explicitly implement `ICovenantEntry<T>`. Internal protocol entries like acknowledgments (`*Ack`) and stream completion markers (`*StreamCompleted`) are intentionally excluded:

- **They are infrastructure concerns**, not semantic entries that need connectivity guarantees
- **They flow through the journal** but don't participate in the domain-level entry graph
- **Adding them would create noise** in covenant definitions without adding value

If you're building custom infrastructure entries that flow through the journal but shouldn't participate in covenant validation, simply don't mark them with `ICovenantEntry<T>`.

## Design

The covenant adds one thing: **marker interfaces that enable static analysis**.

All existing primitives remain unchanged:
- `IScrivener<T>` — the journal
- `IWindowPolicy<T>` — decides when to emit
- `IBatchTransmuter<T,U>` — transforms windows
- `IShatterPolicy<T>` — post-transform split
- `StreamWindowingDaemon` — hosts the pipeline

*"Register a Covenant. The compiler proves it's complete."*
