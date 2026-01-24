# Journal Protocol Isolation

> **Status**: Implemented (runtime validation); Roslyn analyzer planned  
> **Builds on**: [Journaling-and-Scriveners.md](Journaling-and-Scriveners.md), [Windowing-and-Shattering.md](Windowing-and-Shattering.md)

## The One New Concept: Covenant

A **Covenant** is a connectivity guarantee for a journal protocol.

When you define a Covenant, the validator proves:
- Every entry type has a consumer (no dead letters)
- Every consumer has a producer (no orphans)  
- The graph is fully connected (no islands)

That's it. Everything else is composition of existing primitives (`IWindowPolicy`, `IBatchTransmuter`, `IShatterPolicy`, `Daemon`) wired through a builder.

---

## Motivation

The current Scrivener patterns provide powerful, decoupled coordination via append-only journals. However, understanding the flow requires tracing through DI registrations, daemon subscriptions, and transmuter chains. This cognitive overhead creates risk:

- **Dead letters**: A producer writes entries that no consumer ever processes
- **Orphaned consumers**: A daemon tails a journal that no producer ever populates
- **Implicit contracts**: The relationship between entry types and their handlers lives in convention, not code

We are close to a cleaner model. This document proposes **Covenants** — compile-time verifiable journal protocols built by composing existing primitives.

---

## What It Looks Like

```csharp
// Define the covenant — one per protocol
public sealed class ChatCovenant : ICovenant
{
    public static string Name => "Chat";
}

// Register via DI — this is where connectivity is enforced
services.AddCovenant<ChatCovenant>(covenant =>
{
    // Declare boundaries
    covenant.Source<UserMessage>();       // enters from outside
    covenant.Sink<AssistantMessage>();    // exits to outside
    
    // Wire the pipeline (uses existing primitives)
    covenant.Window<ChatChunk, ChatEfferent>(
        policy: new ParagraphWindowPolicy<ChatChunk>(),
        transmuter: new ChatChunkBatchTransmuter());
});
```

**The sentence:** *"Register a Covenant. The compiler proves it's complete."*

---

## Design Principle: Composition, Not Invention

Rather than create new abstractions, Covenants **compose** existing primitives:

| Existing Primitive | Role in Covenant |
|--------------------|------------------|
| `IScrivener<T>` | The journal — unchanged |
| `IWindowPolicy<T>` | Decides when to emit — unchanged |
| `IBatchTransmuter<TIn, TOut>` | Transforms windows — unchanged |
| `IShatterPolicy<T>` | Post-transform split — unchanged |
| `TappedScrivener<T>` | Cross-cutting decorator — unchanged |
| `StreamWindowingDaemon` | Hosts the pipeline — unchanged |

The Covenant adds **one thing**: marker interfaces that enable static analysis.

---

## Marker Interfaces

Plain, descriptive names for the metadata that enables analysis:

```csharp
/// <summary>
/// Defines a journal protocol with compile-time connectivity guarantees.
/// </summary>
public interface ICovenant 
{
    static abstract string Name { get; }
}

/// <summary>
/// Marks an entry type as belonging to a covenant.
/// </summary>
public interface ICovenantEntry<TCovenant> where TCovenant : ICovenant { }

/// <summary>
/// Marks an entry type as entering the covenant from outside.
/// </summary>
public interface ICovenantSource<TCovenant> where TCovenant : ICovenant { }

/// <summary>
/// Marks an entry type as exiting the covenant to outside.
/// </summary>
public interface ICovenantSink<TCovenant> where TCovenant : ICovenant { }
```

Entry types declare their covenant membership:

```csharp
// Entries sealed to the ChatCovenant
public record UserMessage(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>;

public record ChatChunk(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>;

public record ChatEfferent(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>;

public record AssistantMessage(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSink<ChatCovenant>;
```

---

## Compile-Time Guarantees

With marker interfaces in place, a Roslyn analyzer verifies covenant correctness:

### 1. No Dead Letters

Every `ICovenantEntry<C>` must either:
- Be consumed by a registered window/transmuter, OR
- Implement `ICovenantSink<C>`

```csharp
// Analyzer error: ChatEfferent has no consumer and is not a sink
public record ChatEfferent(string Text) : ChatEntry, ICovenantEntry<ChatCovenant>;

// Fixed: mark as sink or add a consumer
public record ChatEfferent(string Text) : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSink<ChatCovenant>;
```

### 2. No Orphaned Consumers

Every window/transmuter input type must either:
- Be produced by another window/transmuter, OR
- Implement `ICovenantSource<C>`

```csharp
// Analyzer error: Window consumes ChatChunk but nothing produces it
covenant.Window<ChatChunk, ChatEfferent>(...);

// Fixed: ChatChunk must be marked as a source
public record ChatChunk(string Text) : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>;
```

### 3. Connectivity

The analyzer builds a graph and verifies:
- Every entry is reachable from a source
- Every entry reaches a sink
- No islands

```
Source ──▶ UserMessage ──▶ [Transform] ──▶ AgentPrompt
                                               │
                                               ▼
                    [Window] ◀── ChatChunk ◀── Source
                       │
                       ▼
                  ChatEfferent ──▶ [Transform] ──▶ AssistantMessage ──▶ Sink
```

---

## The Covenant Builder

The builder is where composition happens. It wires existing primitives and collects metadata for the analyzer:

```csharp
public static class CovenantServiceCollectionExtensions
{
    public static IServiceCollection AddCovenant<TCovenant>(
        this IServiceCollection services,
        Action<ICovenantBuilder<TCovenant>> configure)
        where TCovenant : ICovenant
    {
        var builder = new CovenantBuilder<TCovenant>(services);
        configure(builder);
        builder.Validate(); // Runtime check that static analysis passed
        return services;
    }
}

public interface ICovenantBuilder<TCovenant> where TCovenant : ICovenant
{
    /// <summary>Declare an entry type that enters from outside.</summary>
    void Source<TEntry>() where TEntry : ICovenantEntry<TCovenant>, ICovenantSource<TCovenant>;
    
    /// <summary>Declare an entry type that exits to outside.</summary>
    void Sink<TEntry>() where TEntry : ICovenantEntry<TCovenant>, ICovenantSink<TCovenant>;
    
    /// <summary>Wire a windowing pipeline using existing primitives.</summary>
    void Window<TChunk, TOutput>(
        IWindowPolicy<TChunk> policy,
        IBatchTransmuter<TChunk, TOutput> transmuter,
        IShatterPolicy<TOutput>? shatter = null)
        where TChunk : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>;
    
    /// <summary>Wire a 1:1 transform.</summary>
    void Transform<TInput, TOutput>(
        ITransmuter<TInput, TOutput> transmuter)
        where TInput : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>;
}
```

The generic constraints do the heavy lifting — you can only wire entry types that are actually sealed to this covenant.

---

## Complete Example

```csharp
// ═══════════════════════════════════════════════════════════════
// 1. DEFINE THE COVENANT
// ═══════════════════════════════════════════════════════════════

public sealed class ChatCovenant : ICovenant
{
    public static string Name => "Chat";
}

// ═══════════════════════════════════════════════════════════════
// 2. DECLARE ENTRY TYPES WITH COVENANT MEMBERSHIP
// ═══════════════════════════════════════════════════════════════

// User input enters from outside
public record UserMessage(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>;

// Chunks are internal to the covenant
public record ChatChunk(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>;

// Windowed output
public record ChatEfferent(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>;

// Final output exits to outside  
public record AssistantMessage(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSink<ChatCovenant>;

// ═══════════════════════════════════════════════════════════════
// 3. WIRE IT UP VIA THE BUILDER
// ═══════════════════════════════════════════════════════════════

services.AddCovenant<ChatCovenant>(covenant =>
{
    // Boundaries
    covenant.Source<UserMessage>();
    covenant.Source<ChatChunk>();
    covenant.Sink<AssistantMessage>();
    
    // Windowing pipeline (reuses existing primitives)
    covenant.Window<ChatChunk, ChatEfferent>(
        policy: new ParagraphWindowPolicy<ChatChunk>(),
        transmuter: new ChatChunkBatchTransmuter());
    
    // Final transform
    covenant.Transform<ChatEfferent, AssistantMessage>(
        transmuter: new ChatEfferentToMessageTransmuter());
});
```

The analyzer verifies:
- `UserMessage` is a source ✓
- `ChatChunk` is a source ✓  
- `ChatChunk` → `ChatEfferent` via Window ✓
- `ChatEfferent` → `AssistantMessage` via Transform ✓
- `AssistantMessage` is a sink ✓
- Graph is connected, no islands ✓

---

## TappedScrivener Remains Valid

`TappedScrivener<T>` is orthogonal to Covenants — it provides cross-cutting concerns at the journal boundary:

```csharp
// TappedScrivener decorates the journal, not the covenant
// Still valid for: logging, side-effects, metrics, filtering

internal sealed class DiscordScrivener : TappedScrivener<DiscordEntry>
{
    public override async Task<long> WriteAsync(DiscordEntry entry, CancellationToken ct)
    {
        await _discord.SendAsync(entry);
        return await WriteInnerAsync(entry, ct);
    }
}
```

---

## Open Questions

### Dynamic Covenants

Some flows are determined at runtime (e.g., available tools, registered agents). Static analysis can't verify dynamic registration.

**Possible approach**: 
- Core covenant is static with marker interfaces
- Dynamic portions validated at startup
- Runtime errors for incomplete dynamic graphs

### Performance

The covenant builder and analyzer are compile/startup-time. Runtime behavior is unchanged — still uses `StreamWindowingDaemon`, `IWindowPolicy`, etc.

### Error Handling

Unchanged from current model. Transmuter failures are handled by the daemon. Could add covenant-level dead letter handling as future work.

---

## What Changes

| Current | After Covenants | Notes |
|---------|-----------------|-------|
| `IScrivener<T>` | **Unchanged** | Foundation |
| `IWindowPolicy<T>` | **Unchanged** | Still decides when to emit |
| `IBatchTransmuter<T,U>` | **Unchanged** | Still transforms |
| `IShatterPolicy<T>` | **Unchanged** | Still shatters |
| `TappedScrivener<T>` | **Unchanged** | Still decorates |
| `StreamWindowingDaemon` | **Unchanged** | Still runs pipelines |
| (none) | `ICovenant` | **New**: protocol definition |
| (none) | `ICovenantEntry<T>` | **New**: membership marker |
| (none) | `ICovenantSource<T>` | **New**: boundary marker |
| (none) | `ICovenantSink<T>` | **New**: boundary marker |
| (none) | `AddCovenant<T>()` | **New**: builder/validator |
| (none) | Roslyn Analyzer | **Planned**: compile-time verification |

**Zero changes to runtime behavior.** The covenant is purely a startup-time verification layer (compile-time with future analyzer).

---

## Implementation Status

### ✅ Completed

1. **Marker interfaces** in `Coven.Core.Covenants`:
   - `ICovenant` — protocol definition with static `Name`
   - `ICovenantEntry<T>` — membership marker
   - `ICovenantSource<T>` — boundary in
   - `ICovenantSink<T>` — boundary out
   - `ICovenantBuilder<T>` — base builder interface

2. **Covenant builder** in `Coven.Covenants`:
   - `IStreamingCovenantBuilder<T>` — extended builder with Window/Transform
   - `StreamingCovenantBuilder<T>` — implementation
   - `CovenantServiceCollectionExtensions.AddCovenant<T>()` — DI registration
   - `CovenantValidator` — runtime validation at startup
   - `CovenantGraph<T>` — graph metadata for inspection

3. **ChatCovenant** applied in `Coven.Chat`:
   - `ChatCovenant` — defines the chat protocol
   - `ChatAfferent` — marked as `ICovenantSource<ChatCovenant>`
   - `ChatChunk` — marked as `ICovenantSource<ChatCovenant>`
   - `ChatEfferent` — marked as `ICovenantSink<ChatCovenant>`

### 🔜 Planned

4. **Roslyn analyzer** (new project: `Coven.Covenants.Analyzers`):
   - Verify all `ICovenantEntry<C>` have consumers or are sinks
   - Verify all consumers have producers or are sources
   - Verify connectivity (no islands)
   - Shift validation from startup to compile-time

5. **Additional covenants**:
   - `AgentCovenant` for agent flows

---

## Summary

**Covenant** is the one new concept: a connectivity guarantee for journal protocols.

Everything else is composition of existing primitives:
- `IWindowPolicy<T>` — decides when to emit
- `IBatchTransmuter<T,U>` — transforms windows
- `IShatterPolicy<T>` — splits outputs
- `StreamWindowingDaemon` — runs the pipeline

The covenant adds:
- Marker interfaces for static analysis
- A builder for DI registration
- Runtime validation at startup (Roslyn analyzer planned)

**The sentence:** *"Register a Covenant. The validator proves it's complete."*
