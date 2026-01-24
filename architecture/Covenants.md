# Covenants

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

A covenant defines the boundaries and internal wiring of a journal protocol:

```
                    ┌─────────────────────────────────────┐
                    │           ChatCovenant              │
                    ├─────────────────────────────────────┤
   SOURCES          │                                     │          SINKS
   ────────►        │    ChatAfferent ──► [Window] ──►    │        ────────►
   ChatAfferent     │                         │           │        ChatEfferent
   ChatChunk        │    ChatChunk ───────────┘           │
                    │                                     │
                    └─────────────────────────────────────┘
```

For working implementations, see:
- **Covenant definition**: [`ChatCovenant`](../src/Coven.Chat/ChatCovenant.cs)
- **Entry types with markers**: [`ChatEntry.cs`](../src/Coven.Chat/ChatEntry.cs) — `ChatAfferent` (source), `ChatChunk` (source), `ChatEfferent` (sink)
- **DI wiring**: [`CovenantServiceCollectionExtensions`](../src/Coven.Covenants/CovenantServiceCollectionExtensions.cs)

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

Four marker interfaces define covenant membership and boundaries:

| Interface | Purpose | Source |
|-----------|---------|--------|
| `ICovenant` | Protocol definition with static `Name` | [`ICovenant.cs`](../src/Coven.Core/Covenants/ICovenant.cs) |
| `ICovenantEntry<T>` | Marks an entry as belonging to a covenant | [`ICovenantEntry.cs`](../src/Coven.Core/Covenants/ICovenantEntry.cs) |
| `ICovenantSource<T>` | Entry enters from outside (user input, external systems) | [`ICovenantSource.cs`](../src/Coven.Core/Covenants/ICovenantSource.cs) |
| `ICovenantSink<T>` | Entry exits to outside (sent to users, external systems) | [`ICovenantSink.cs`](../src/Coven.Core/Covenants/ICovenantSink.cs) |

For a working example of entry types with covenant markers, see [`ChatEntry.cs`](../src/Coven.Chat/ChatEntry.cs):
- `ChatAfferent` — source (incoming messages)
- `ChatChunk` — source (streaming chunks from AI)
- `ChatEfferent` — sink (outgoing messages)

---

## Validation Guarantees

The validator runs at startup and verifies covenant correctness:

### 1. No Dead Letters

Every `ICovenantEntry<C>` must either:
- Be consumed by a registered window/transmuter/junction, OR
- Implement `ICovenantSink<C>`

```text
# pseudocode — validation examples

❌ ChatEfferent: entry, NOT sink, no consumer registered
   → VALIDATION ERROR: dead letter (nothing reads it)

✓ ChatEfferent: entry, sink
   → OK: boundary marker declares "this leaves the covenant"

✓ ChatEfferent: entry, consumed by Transform → AssistantMessage
   → OK: has a downstream consumer
```

### 2. No Orphaned Consumers

Every window/transmuter/junction input type must either:
- Be produced by another operation, OR
- Implement `ICovenantSource<C>`

```text
# pseudocode — validation examples

❌ Window consumes ChatChunk, but ChatChunk is NOT source, no producer
   → VALIDATION ERROR: orphaned consumer (nothing writes it)

✓ ChatChunk: entry, source
   → OK: boundary marker declares "this enters from outside"

✓ ChatChunk: produced by Transform ← RawInput
   → OK: has an upstream producer
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

| Arity | Operation | Input → Output | Purpose |
|-------|-----------|----------------|----------|
| 1 | `Source<T>` | ∅ → T | Declare entry point (data enters covenant) |
| 1 | `Sink<T>` | T → ∅ | Declare exit point (data leaves covenant) |
| 2 | `Window<TChunk, TOut>` | TChunk* → TOut | Batch chunks via policy, then transmute |
| 2 | `Transform<TIn, TOut>` | TIn → TOut | 1:1 transformation |
| 2+ | `Junction<TIn>` | TIn → TOut₁ \| TOut₂ \| ... | Route to multiple outputs by predicate |

```text
# pseudocode — arity patterns

1-ary (boundaries):
  Source<UserMessage>         ∅ ──────► UserMessage
  Sink<AssistantMessage>      AssistantMessage ──────► ∅

2-ary (transforms):
  Window<Chunk, Efferent>     Chunk* ══► [policy+transmute] ══► Efferent
  Transform<Efferent, Msg>    Efferent ──► [transmute] ──► Msg

3+-ary (fan-out via Junction):
  Junction<AgentEntry>        AgentEntry ──┬──► AgentPrompt
                                           ├──► AgentResponse  
                                           └──► AgentThought
```

The validator verifies connectivity across all arities — every output must reach a consumer, every input must have a producer.

For the full builder API, see [`IStreamingCovenantBuilder.cs`](../src/Coven.Covenants/IStreamingCovenantBuilder.cs).

---

## The Covenant Builder

The builder wires existing primitives and collects metadata for validation.

**Key implementation files:**
- [`CovenantServiceCollectionExtensions.cs`](../src/Coven.Covenants/CovenantServiceCollectionExtensions.cs) — `AddCovenant<T>()` extension method
- [`IStreamingCovenantBuilder.cs`](../src/Coven.Covenants/IStreamingCovenantBuilder.cs) — builder interface with `Source`, `Sink`, `Window`, `Transform`, `Junction`
- [`CovenantValidator.cs`](../src/Coven.Covenants/CovenantValidator.cs) — startup validation logic

```text
# pseudocode — builder flow

AddCovenant<MyCovenant>(services, configure):
  1. create builder with empty graph
  2. call configure(builder)  →  user registers operations
  3. builder.Validate()       →  throws if graph invalid
  4. return services          →  DI container ready
```

The generic constraints do the heavy lifting — you can only wire entry types that are actually sealed to this covenant.

---

## Complete Example: ChatCovenant

The ChatCovenant demonstrates the full pattern. For working code, see the actual implementations linked below.

### Data Flow

```text
┌─────────────────────────────────────────────────────────────────┐
│                        ChatCovenant                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐                                                   │
│  │ SOURCE   │                                                   │
│  │ UserMsg  │──────────────────────────────────────┐            │
│  └──────────┘                                      │            │
│                                                    ▼            │
│  ┌──────────┐      ┌────────────┐      ┌───────────────────┐    │
│  │ SOURCE   │      │  Window    │      │    Transform      │    │
│  │ ChatChunk│─────►│ + Transmute│─────►│ Efferent → Msg    │    │
│  └──────────┘      └────────────┘      └─────────┬─────────┘    │
│       *chunks*         *batch*                   │              │
│                                                  ▼              │
│                                           ┌──────────┐          │
│                                           │  SINK    │          │
│                                           │ AsstMsg  │          │
│                                           └──────────┘          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Implementation Files

| Step | What | Where |
|------|------|-------|
| 1. Define covenant | Covenant type with `ICovenant` marker | [`ChatCovenant.cs`](../src/Coven.Chat/ChatCovenant.cs) |
| 2. Declare entries | Entry types with `ICovenantEntry`, `ICovenantSource`, `ICovenantSink` | [`ChatEntry.cs`](../src/Coven.Chat/ChatEntry.cs) |
| 3. Wire via builder | `AddCovenant<ChatCovenant>(...)` registration | See `Coven.Chat` service registration |

### What the Validator Checks

```text
✓ UserMessage    →  source marker present
✓ ChatChunk      →  source marker present
✓ ChatChunk      →  consumed by Window → ChatEfferent
✓ ChatEfferent   →  consumed by Transform → AssistantMessage
✓ AssistantMessage → sink marker present
✓ Graph connected  →  no islands, all paths source→sink
```
- `AssistantMessage` is a sink ✓
- Graph is connected, no islands ✓

---

## TappedScrivener Remains Valid

`TappedScrivener<T>` is orthogonal to Covenants — it provides cross-cutting concerns at the journal boundary.

**Use cases:** logging, side-effects, metrics, filtering.

**Relationship to covenants:** A `TappedScrivener` decorates the *journal*, not the *covenant*. Covenants verify connectivity between entry types; tapped scriveners intercept writes regardless of covenant membership.

For details on tapped scriveners, see [Journaling-and-Scriveners.md](Journaling-and-Scriveners.md).

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
