# Proposal: Flat Covenant Model

> **Status**: Draft  
> **Created**: 2026-01-31

## Summary

Eliminate the inner/outer covenant distinction. One covenant, one graph, one scope. Branches connect to the covenant regardless of their internal complexity. "Composite" becomes a branch registration pattern, not a separate runtime model.

## Motivation

The current model has two parallel implementations:

| Concept | Outer | Inner |
|---------|-------|-------|
| Builder | `CovenantBuilder` | `InnerCovenantBuilder` |
| Runtime | `CovenantAdherentDaemon` | Manual pump loop in `CompositeDaemon` |
| Scope | `IServiceScope` from DI | Fresh `ServiceProvider` |
| Scriveners | Configured at registration | Hardcoded `InMemoryScrivener` |

This creates vocabulary confusion and code duplication. But tracing through the actual DiscordAgent sample reveals the runtime is simple:

```
1. Ritual() called
2. CovenExecutionScope creates IServiceScope
3. Resolves IEnumerable<IDaemon> — all daemons in one flat list
4. Starts all daemons (including CovenantAdherentDaemon)
5. CovenantAdherentDaemon runs pumps that tail scriveners
6. Shutdown in reverse order
```

There's no hierarchy. No nested scopes. Just services, daemons, scriveners, and routes.

A "composite" should work the same way: register its daemons and scriveners, connect its manifest, define routes. One graph.

## Design

### The Model

```
services.BuildCoven(coven => {
    var chat = coven.UseDiscordChat(config);
    var agents = coven.UseOpenAIAgents(config);
    var spellcasting = coven.UseSpellcasting(config);  // just another branch
    
    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Connect(spellcasting)
        .Routes(c => {
            c.Route<ChatAfferent, AgentPrompt>(...);
            c.Route<AgentResponse, SpellRequest>(...);
            c.Route<FileContent, SpellResult>(...);
            c.Route<SpellResult, ChatEfferentDraft>(...);
            c.Terminal<AgentThought>();
        });
});
```

One covenant. One flat graph. All branches are peers.

### What a Branch Is

A branch registers:
- Scriveners (journals for its entry types)
- Daemons (services that read/write those journals)
- A manifest declaring what it produces and consumes

That's it. A branch doesn't know about other branches. The covenant connects them.

### What the Covenant Does

The covenant:
1. Collects manifests from connected branches
2. Validates routes cover all produced/consumed types
3. Builds pumps (tail source → transform → write target)
4. Registers `CovenantDescriptor` and `CovenantAdherentDaemon`

At runtime, `CovenantAdherentDaemon` runs the pumps. Entries flow through the graph.

### What "Spellcasting" Becomes

Today's `UseSpellcasting` would create a composite with inner covenants. In the flat model:

```
public static BranchManifest UseSpellcasting(this CovenServiceBuilder coven, SpellConfig config)
{
    // Register spellcasting services
    coven.Services.AddScoped<IScrivener<SpellEntry>, InMemoryScrivener<SpellEntry>>();
    coven.Services.AddScoped<IScrivener<FileEntry>, InMemoryScrivener<FileEntry>>();
    coven.Services.AddScoped<ContractDaemon, FileSystemDaemon>();
    
    // Return manifest — what this branch produces/consumes
    return new BranchManifest(
        Name: "Spellcasting",
        JournalEntryType: typeof(SpellEntry),
        Produces: { typeof(SpellResult), typeof(FileContent) },
        Consumes: { typeof(SpellRequest), typeof(FileReadOp) },
        RequiredDaemons: [typeof(ContractDaemon)]);
}
```

The caller's covenant routes to/from spellcasting types just like any other branch. No `CompositeDaemon`. No inner covenant. Just more entries in the same graph.

### Multiple Journal Types per Branch

A branch can work with multiple journal types. The manifest declares its primary journal type, but daemons can write to any scrivener they inject.

If spellcasting internally has `FileEntry` and `ComputeEntry`, those are just more scriveners registered in DI. The covenant routes between all entry types in one graph.

### Validation

One set of rules for all covenants:

| Rule | Description |
|------|-------------|
| Coverage | Every `manifest.Produces` has a route or terminal |
| Uniqueness | Each source type has at most one route |
| Route/Terminal exclusion | A type cannot have both a route and a terminal |
| Consumer satisfaction | Every `manifest.Consumes` has a route producing it |
| Transmuter registration | Transmuter types must be in DI |
| Entry-journal uniqueness | No entry type in multiple journals |

Note: Entry-journal uniqueness validation exists only in `InnerCovenantBuilder` today. Port it to `CovenantBuilder` as part of this work.

## Implementation Notes

### Validation to port

`CovenantBuilder` lacks entry-journal uniqueness validation. Currently crashes with unhelpful `ArgumentException` from `ToDictionary`. Port the explicit check from `InnerCovenantBuilder`:

```
Entry type {X} appears in multiple journals: {sources}.
Each entry type must belong to exactly one journal.
```

### Daemon registration forwarding

`CovenantBuilder.RegisterDaemons()` forwards `ContractDaemon` registrations to `IDaemon`. This stays — it's how `CovenExecutionScope` discovers branch daemons.

### Ordering

`CovenantAdherentDaemon` runs pumps that tail scriveners. It should start after branch daemons that write initial entries. Current registration order handles this (covenant is built after `Use*` calls).

### Thread safety

`IScrivener` implementations must be thread-safe. Multiple pumps write concurrently. `InMemoryScrivener` uses `ConcurrentQueue` — this is correct.

## Implementation Plan

### Phase 1: Flatten existing composites

1. Convert `UseSpellcasting` (if it exists) to flat branch pattern
2. Register all scriveners and daemons directly in DI
3. Connect as peer branch, route in main covenant

### Phase 2: Delete composite infrastructure

1. Delete `InnerCovenantBuilder`
2. Delete `CompositeDaemon`
3. Delete `CompositeBranchManifest`
4. Remove `InnerManifests` and `InnerPumps` concepts

### Phase 3: Simplify CovenantBuilder

1. Remove `Connect(CompositeBranchManifest)` overload
2. Keep only `Connect(BranchManifest)`
3. One validation path, one pump-building path

## Trade-offs

### Advantages

- **Minimal vocabulary** — branch, manifest, covenant, route
- **One runtime model** — `CovenExecutionScope` starts daemons, `CovenantAdherentDaemon` runs pumps
- **~600 lines deleted** — replaced by nothing

### What We're Deleting

| Type | Lines |
|------|-------|
| `InnerCovenantBuilder` | 316 |
| `CompositeDaemon` | 223 |
| `IInnerCovenantBuilder` | 40 |
| `CompositeBranchManifest` | 24 |
| Tests for above | ~500 |

### Properties Not Preserved

These existed in `CompositeDaemon` but were not planned features:

- **Scope isolation** — inner scriveners in separate `ServiceProvider`
- **Atomic startup** — composite "Running" only when all inner parts ready
- **Entry type encapsulation** — inner types hidden from outer routes

None of these are used. They were accidental complexity from the parallel implementation.

## Migration

No external consumers. Delete the code, delete the tests.

## Open Questions

None. The research confirmed:
1. Zero production code uses composite infrastructure
2. The flat model matches how the runtime already works
3. Lost properties were accidental, not designed

## Alternatives Considered

### A. Nested scopes with unified builder

**Rejected**: Runtime doesn't need it. Adds complexity for no benefit.

### B. Keep CompositeDaemon, simplify builder only

**Rejected**: CompositeDaemon reimplements what CovenExecutionScope already does.

### C. Organizational sub-graphs that flatten at build time

**Rejected**: If it's flattened anyway, write it flat.
