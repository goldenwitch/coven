# Covenants and Routing

Covenants define how journal entries flow between branches at build time. At runtime, the `CovenantAdherentDaemon` executes these routes by tailing source journals, applying transformations, and writing results to target journals.

## Core Concepts

### The Routing Model

A covenant describes the canonical path entries take through the system. Rather than writing imperative journal-tailing code, you declare routes:

```csharp
coven.Covenant()
    .Connect(chat)
    .Connect(agents)
    .Routes(c =>
    {
        c.Route<ChatAfferent, AgentPrompt>((msg, ct) => 
            Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));
        
        c.Route<AgentResponse, ChatEfferentDraft>((r, ct) => 
            Task.FromResult(new ChatEfferentDraft("BOT", r.Text)));
        
        c.Terminal<AgentThought>();
    });
```

Each produced entry type must have exactly one disposition: a `Route` or a `Terminal`. Terminals explicitly mark types that should not be routed anywhere—no implicit ignoring allowed.

### Declarative vs Imperative

**Declarative (covenants)**: Routes are defined at DI time. Build-time validation ensures completeness. The runtime infrastructure handles journal tailing, entry dispatch, and writing.

**Imperative (RouterBlock)**: Manual journal tailing with pattern matching. No validation. Useful when routing logic requires state or complex conditional flows that don't fit the declarative model.

The declarative approach eliminates boilerplate while the imperative approach remains available for edge cases.

## Branch Manifests

Each branch declares what it produces and consumes via a `BranchManifest`:

```csharp
public sealed record BranchManifest(
    string Name,
    Type JournalEntryType,
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<Type> RequiredDaemons);
```

**`JournalEntryType`**: The base entry type for the branch's journal (e.g., `typeof(ChatEntry)`). Required for scrivener resolution—routes specify leaf types like `ChatAfferent`, but scriveners are registered for base journal types like `IScrivener<ChatEntry>`.

**`Produces`**: Entry types the branch writes (outputs from the branch's perspective).

**`Consumes`**: Entry types the branch reads and acts upon (inputs to the branch).

**`RequiredDaemons`**: Daemon types that must be started for the branch to function.

## Build-Time Validation

When `Routes()` completes, the `CovenantBuilder` validates:

1. **Coverage**: Every type in any manifest's `Produces` has a Route or Terminal
2. **No duplicates**: Each source type has at most one route
3. **Consumers satisfied**: Every type in any manifest's `Consumes` has a Route producing it

Validation failures throw `CovenantValidationException` with actionable error messages.

## Runtime Execution

### Pump Architecture

For each route, the daemon spawns a **pump**—a long-running task that:
1. Tails the source journal
2. Filters entries to the route's source type (exact type match)
3. Applies the transformation
4. Writes results to the target journal

```
┌─────────────────┐     ┌───────────┐     ┌─────────────────┐
│ Source Journal  │────▶│   Pump    │────▶│ Target Journal  │
│ (ChatEntry)     │     │ (filter + │     │ (AgentEntry)    │
│                 │     │ transform)│     │                 │
└─────────────────┘     └───────────┘     └─────────────────┘
```

**Concurrency model**: Each route runs an independent pump. Multiple pumps may tail the same source journal with independent cursors, maximizing isolation.

### Two-Phase Type Capture

Type information is captured at two distinct moments to avoid runtime reflection:

**Phase 1 — Route definition** (`c.Route<TSource, TTarget>(...)`): Captures leaf entry types and the transformation in a typed closure. The result is a `RouteDescriptor` with a type-erased invoker (`Func<Entry, CancellationToken, Task<Entry>>`).

**Phase 2 — Pump construction** (when `Routes()` completes): With all manifests available, builds a mapping from leaf types to journal types. Uses one-time reflection (`MakeGenericMethod`) to create `PumpDescriptor` instances with fully-typed scrivener access baked into closures.

```csharp
internal sealed record PumpDescriptor(
    Type SourceType,
    Type TargetType,
    Func<IServiceProvider, CancellationToken, Task> CreatePump);
```

At runtime, the daemon simply executes pre-built pump factories—no type resolution, no reflection.

### Scrivener Resolution

Routes specify leaf entry types (`ChatAfferent`), but scriveners are registered for base journal types (`IScrivener<ChatEntry>`). The `JournalEntryType` in `BranchManifest` bridges this gap:

```csharp
// Build entry-to-journal lookup from manifests
Dictionary<Type, Type> entryToJournal = manifests
    .SelectMany(m => m.Produces.Concat(m.Consumes)
        .Select(t => (Entry: t, Journal: m.JournalEntryType)))
    .ToDictionary(x => x.Entry, x => x.Journal);
```

### Source Type Filtering

A scrivener for `ChatEntry` yields all subtypes. The pump filters to process only entries matching the route's source type:

```csharp
await foreach ((long _, TSourceJournal entry) in source.TailAsync(0, ct))
{
    if (entry.GetType() != sourceLeafType)
        continue;
    // ... transform and write
}
```

## Error Handling

**Fail-fast philosophy**: Pure transformations have no transient failures—they either succeed or fail due to bugs or bad data. If a transformation throws, the daemon fails. This surfaces bugs immediately rather than silently dropping entries or retrying pointlessly.

**Type safety**: Casts within pumps are verifiable at pump construction time. If a route produces the wrong type, it fails fast with `InvalidCastException`.

## Terminals

Terminal types are explicitly not routed. They exist in the journal as endpoints—useful for logging, debugging, or types that represent internal state (like `AgentThought`).

Terminals require explicit declaration; the covenant does not implicitly ignore any produced type. This prevents accidental data loss from forgotten routes.

## Composite Branches

A composite branch encapsulates an inner covenant, exposing only a boundary to the outer world.

### When to Use

Use composite branches when:
- A branch needs internal sub-routing (e.g., Spellcasting dispatching to FileSystem/Compute)
- You want build-time validation of internal routes
- Inner structure should be opaque to consumers

### Declaration

```csharp
CompositeBranchManifest spellcasting = coven.CompositeManifest<SpellEntry, SpellcastingDaemon>(
    "Spellcasting",
    produces: new HashSet<Type> { typeof(SpellResult), typeof(SpellFault) },
    consumes: new HashSet<Type> { typeof(FileReadSpell), typeof(ShellExecSpell) },
    inner =>
    {
        BranchManifest filesystem = inner.Branch("FileSystem", typeof(FileSystemEntry), ...);
        
        inner.ConnectBoundary();
        inner.Connect(filesystem);
        
        inner.Routes(c =>
        {
            c.Route<FileReadSpell, FileRead>(...);
            c.Route<FileContent, SpellResult>(...);
        });
    });

coven.Covenant()
    .Connect(chat)
    .Connect(spellcasting)  // Looks like any other branch
    .Routes(...);
```

### Validation

Inner covenants get the same validation as outer covenants, plus boundary coherence:

1. Every boundary `produces` type must be a route target
2. Every boundary `consumes` type must be a route source
3. No dead letters—inner produces must route somewhere

Validation runs at `BuildCoven()` time. Failures throw `CovenantValidationException`.

### Runtime

`CompositeDaemon<TBoundary>` manages inner infrastructure:

- Creates child service scope with inner scriveners
- Instantiates and starts inner daemons
- Runs inner covenant pumps
- Fails fast if any inner daemon faults

## Windowing Relationship

Covenants operate on individual entries. By the time entries reach the covenant, windowing decisions are complete. The covenant does not buffer, aggregate, or decide "when" to emit—that's the windowing layer's responsibility, upstream.

When streaming is enabled, chunk types appear in the manifest's `Produces`. The covenant routes them like any other entry type:

```csharp
// Raw chunks (for real-time display)
c.Route<AgentAfferentChunk, ChatChunk>(...);

// Completed entries (post-windowing)
c.Route<AgentResponse, ChatEfferentDraft>(...);
```

## DI Registration

Both `CovenantDescriptor` and `CovenantAdherentDaemon` are registered as **scoped**:

- **Scoped descriptor**: Different `DaemonScope` instances may operate under different covenants (e.g., test vs production routes)
- **Scoped daemon**: Daemons are tied to scope lifetime—each scope gets fresh daemon instances

The daemon is auto-started by `DaemonScope.BeginScopeAsync()` alongside other daemons.

## Route Types

### Lambda Routes

Direct transformation via async lambda:

```csharp
c.Route<ChatAfferent, AgentPrompt>((msg, ct) => 
    Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));
```

### Transmuter Routes

DI-resolved transmuter for reusable, testable transformations:

```csharp
c.Route<ChatAfferent, AgentPrompt, ChatToAgentTransmuter>();
```

The transmuter is resolved from the service provider when the pump starts.

## See Also

- [Journaling and Scriveners](./Journaling-and-Scriveners.md) — Journal fundamentals
- [Abstractions and Branches](./Abstractions-and-Branches.md) — Branch architecture
- [Windowing and Shattering](./Windowing-and-Shattering.md) — Semantic readiness policies
- Package docs: `src/Coven.Core/README.md`
