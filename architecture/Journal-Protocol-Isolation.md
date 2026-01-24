# Journal Protocol Isolation

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

The Scrivener patterns provide powerful, decoupled coordination via append-only journals. However, understanding the flow requires tracing through DI registrations, daemon subscriptions, and transmuter chains. This cognitive overhead creates risk:

- **Dead letters**: A producer writes entries that no consumer ever processes
- **Orphaned consumers**: A daemon tails a journal that no producer ever populates
- **Implicit contracts**: The relationship between entry types and their handlers lives in convention, not code

**Covenants** solve this — verifiable journal protocols built by composing existing primitives.

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
    // Declare boundaries (1-ary operations)
    covenant.Source<UserMessage>();       // enters from outside
    covenant.Sink<AssistantMessage>();    // exits to outside
    
    // Wire the pipeline (2-ary operations)
    covenant.Window<ChatChunk, ChatEfferent>(
        policy: new ParagraphWindowPolicy<ChatChunk>(),
        transmuter: new ChatChunkBatchTransmuter());
});
```

**The sentence:** *"Register a Covenant. The validator proves it's complete."*

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

The Covenant adds **one thing**: marker interfaces that enable validation.

---

## Marker Interfaces

Plain, descriptive names for the metadata that enables analysis:

```csharp
/// <summary>
/// Defines a journal protocol with connectivity guarantees.
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

## Validation Guarantees

The validator runs at startup and verifies covenant correctness:

### 1. No Dead Letters

Every `ICovenantEntry<C>` must either:
- Be consumed by a registered window/transmuter/junction, OR
- Implement `ICovenantSink<C>`

```csharp
// Validation error: ChatEfferent has no consumer and is not a sink
public record ChatEfferent(string Text) : ChatEntry, ICovenantEntry<ChatCovenant>;

// Fixed: mark as sink or add a consumer
public record ChatEfferent(string Text) : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSink<ChatCovenant>;
```

### 2. No Orphaned Consumers

Every window/transmuter/junction input type must either:
- Be produced by another operation, OR
- Implement `ICovenantSource<C>`

```csharp
// Validation error: Window consumes ChatChunk but nothing produces it
covenant.Window<ChatChunk, ChatEfferent>(...);

// Fixed: ChatChunk must be marked as a source
public record ChatChunk(string Text) : ChatEntry, ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>;
```

### 3. Connectivity

The validator builds a graph and verifies:
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

## Builder Operation Arities

The covenant builder provides operations at different arities (number of type parameters):

### 1-Ary Operations (Single Type)

Boundary declarations — one entry type in, nothing out:

```csharp
covenant.Source<UserMessage>();    // declares entry point
covenant.Sink<AssistantMessage>(); // declares exit point
```

### 2-Ary Operations (Two Types)

Transform operations — one type in, one type out:

```csharp
// Window: TChunk → TOutput
covenant.Window<ChatChunk, ChatEfferent>(policy, transmuter);

// Transform: TInput → TOutput  
covenant.Transform<ChatEfferent, AssistantMessage>(transmuter);

// Junction with single route: TIn → TOut
covenant.Junction<AgentEntry>(j => j
    .Route<AgentPrompt>(e => e is AgentPrompt, e => (AgentPrompt)e));
```

### 3-Ary Operations (Three Types)

Junction fan-out — one type in, multiple types out:

```csharp
// Junction: TIn → TOut1 | TOut2 | TOut3
covenant.Junction<AgentEntry>(j => j
    .Route<AgentPrompt>(e => e is AgentPrompt, e => (AgentPrompt)e)
    .Route<AgentResponse>(e => e is AgentResponse, e => (AgentResponse)e)
    .Route<AgentThought>(e => e is AgentThought, e => (AgentThought)e));
```

The validator verifies connectivity across all arities — every output must reach a consumer, every input must have a producer.

---

## The Covenant Builder

The builder wires existing primitives and collects metadata for validation:

```csharp
public static class CovenantServiceCollectionExtensions
{
    public static IServiceCollection AddCovenant<TCovenant>(
        this IServiceCollection services,
        Action<IStreamingCovenantBuilder<TCovenant>> configure)
        where TCovenant : ICovenant
    {
        var builder = new StreamingCovenantBuilder<TCovenant>(services);
        configure(builder);
        builder.Validate(); // Throws if graph is invalid
        return services;
    }
}
```

The builder interface:

```csharp
public interface IStreamingCovenantBuilder<TCovenant> where TCovenant : ICovenant
{
    // 1-ary: boundaries
    IStreamingCovenantBuilder<TCovenant> Source<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSource<TCovenant>;
    
    IStreamingCovenantBuilder<TCovenant> Sink<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSink<TCovenant>;
    
    // 2-ary: transforms
    IStreamingCovenantBuilder<TCovenant> Window<TChunk, TOutput>(
        IWindowPolicy<TChunk> policy,
        IBatchTransmuter<TChunk, TOutput> transmuter,
        IShatterPolicy<TOutput>? shatter = null)
        where TChunk : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>;
    
    IStreamingCovenantBuilder<TCovenant> Transform<TInput, TOutput>(
        ITransmuter<TInput, TOutput> transmuter)
        where TInput : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>;
    
    // 3-ary (via nested builder): junction fan-out
    IStreamingCovenantBuilder<TCovenant> Junction<TIn>(
        Action<IJunctionBuilder<TCovenant, TIn>> configure)
        where TIn : ICovenantEntry<TCovenant>;
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
    // 1-ary: boundaries
    covenant.Source<UserMessage>();
    covenant.Source<ChatChunk>();
    covenant.Sink<AssistantMessage>();
    
    // 2-ary: windowing pipeline
    covenant.Window<ChatChunk, ChatEfferent>(
        policy: new ParagraphWindowPolicy<ChatChunk>(),
        transmuter: new ChatChunkBatchTransmuter());
    
    // 2-ary: final transform
    covenant.Transform<ChatEfferent, AssistantMessage>(
        transmuter: new ChatEfferentToMessageTransmuter());
});
```

The validator verifies:
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

Some flows are determined at runtime (e.g., available tools, registered agents). Static validation can't verify dynamic registration.

**Current approach**: 
- Core covenant is static with marker interfaces
- Dynamic portions validated at startup
- Runtime errors for incomplete dynamic graphs

### Error Handling

Unchanged from current model. Transmuter failures are handled by the daemon. Covenant-level dead letter handling is possible future work.

---

## What Changes

| Current | With Covenants | Notes |
|---------|----------------|-------|
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

**Zero changes to runtime behavior.** The covenant is purely a startup-time verification layer.

---

## Implementation

### Marker Interfaces (`Coven.Core.Covenants`)

- `ICovenant` — protocol definition with static `Name`
- `ICovenantEntry<T>` — membership marker
- `ICovenantSource<T>` — boundary in
- `ICovenantSink<T>` — boundary out
- `ICovenantBuilder<T>` — base builder interface

### Covenant Builder (`Coven.Covenants`)

- `IStreamingCovenantBuilder<T>` — extended builder with Window/Transform/Junction
- `StreamingCovenantBuilder<T>` — implementation
- `IJunctionBuilder<T,TIn>` — junction route configuration
- `JunctionBuilder<T,TIn>` — implementation
- `CovenantServiceCollectionExtensions.AddCovenant<T>()` — DI registration
- `CovenantValidator` — runtime validation at startup
- `CovenantGraph<T>` — graph metadata for inspection

### Applied Covenants

- `ChatCovenant` in `Coven.Chat` — defines the chat protocol
- `AgentCovenant` in `Coven.Agents` — defines the agent protocol

---

## Summary

**Covenant** is the one new concept: a connectivity guarantee for journal protocols.

Everything else is composition of existing primitives:
- `IWindowPolicy<T>` — decides when to emit
- `IBatchTransmuter<T,U>` — transforms windows
- `IShatterPolicy<T>` — splits outputs
- `StreamWindowingDaemon` — runs the pipeline

The covenant adds:
- Marker interfaces for validation
- A builder for DI registration with 1-ary, 2-ary, and 3-ary operations
- Runtime validation at startup

**The sentence:** *"Register a Covenant. The validator proves it's complete."*
