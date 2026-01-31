# Inner Covenants

> **Status**: Draft  
> **Created**: 2026-01-30

---

## Summary

An **inner covenant** allows a branch to encapsulate its own sub-graph. The outer world sees only the boundary journal; internal branches, journals, and routing are opaque.

This proposal introduces:
- `CompositeBranchManifest` — declares a boundary plus an inner covenant
- `CompositeDaemon` — owns inner scriveners, runs inner routing pumps, manages child daemons

---

## Motivation

Spellcasting needs to expose a simple boundary (`SpellInvocation` → `SpellResult | SpellFault`) while internally routing to substrate-specific leaves (FileSystem, Compute). Without inner covenants, we'd hand-roll the dispatch logic, losing:

- Build-time validation of internal routes
- Consistent mental model (covenants all the way down)
- Visibility for tooling and debugging

---

## Design

### Composite Branch Manifest

A composite branch declares its boundary and its inner structure:

```
COMPOSITE-MANIFEST Spellcasting
  -- Boundary (what outer covenant sees)
  boundary-journal: SpellEntry
  produces: SpellResult, SpellFault
  consumes: SpellInvocation
  
  -- Inner branches (each has its own journal)
  inner-branches:
    - FileSystem (journal: FileSystemEntry)
    - Compute (journal: ComputeEntry)
  
  -- Inner covenant (routes between boundary ↔ inner branches)
  inner-covenant:
    -- Dispatch: boundary → inner
    ROUTE FileReadSpell → FileRead
    ROUTE FileWriteSpell → FileWrite
    ROUTE ShellExecSpell → ShellExec
    
    -- Gather: inner → boundary
    ROUTE FileContent → SpellResult
    ROUTE FileFault → SpellFault
    ROUTE ShellOutput → SpellResult
    ROUTE ShellFault → SpellFault
  
  -- Inner daemons (run inside the composite)
  inner-daemons: FileSystemDaemon, ComputeDaemon
```

The outer covenant connects to `Spellcasting` like any other branch—it has no visibility into the inner structure.

### Composite Daemon

The `CompositeDaemon` is the runtime host for the inner sub-graph:

```
CompositeDaemon<TBoundary>
  -- Owns inner infrastructure
  boundary-scrivener: IScrivener<TBoundary>        (shared with outer)
  inner-scriveners: IScrivener<TInner>...          (created, not from DI)
  inner-daemons: IDaemon[]                         (instantiated as children)
  inner-pumps: Task[]                              (routing tasks)

  START:
    1. Create inner scriveners (InMemoryScrivener or configured)
    2. Instantiate inner daemons with inner scriveners
    3. Start inner daemons
    4. Start inner covenant pumps (boundary ↔ inner routing)
    5. Transition to Running

  SHUTDOWN:
    1. Cancel inner pumps
    2. Shutdown inner daemons (reverse order)
    3. Transition to Completed
```

The composite daemon appears as a single daemon to the outer scope. Its children are invisible.

### Build-Time Validation

Inner covenants get the same validation as outer covenants, plus boundary coherence:

1. **Coverage**: Every type in any inner manifest's `Produces` must have a `Route` or `Terminal`
2. **Uniqueness**: Each source type has at most one route
3. **Consumer satisfaction**: Every type in any inner manifest's `Consumes` must have a route producing it
4. **Boundary coherence**: 
   - Every type in the composite's `produces` must be a route target from some inner branch
   - Every type in the composite's `consumes` must be a route source to some inner branch

Validation runs at `BuildCoven()` time, before any daemon starts. **If validation fails, the application does not start.**

```
// Example validation error
CovenantValidationException: 
  SpellResult is declared in Spellcasting.produces but no inner route targets it.
  Add: inner.Route<SomeInnerType, SpellResult>(...)
```

### Metagraph Publishing (Optional)

Composites can optionally publish metadata to the metagraph for introspection:

```
COMPOSITE-MANIFEST Spellcasting
  -- ... boundary, inner branches, etc.
  
  metagraph:
    capabilities:
      - type: FileReadSpell
        schema: { path: string }
        description: "Read contents of a file"
```

This is purely optional. The composite works without it. Consumers (like agents) can query this metadata if they want dynamic discovery, or they can hardcode their knowledge of the composite's types.

See [Metagraph](metagraph.md) for details on capability discovery.

---

## Example: Spellcasting

### Outer Covenant (Application)

```
COVENANT outer
  CONNECT chat
  CONNECT agents
  CONNECT spellcasting   -- composite, but looks like any branch
  
  ROUTE ChatAfferent → AgentPrompt
  ROUTE AgentToolCall → SpellInvocation
  ROUTE SpellResult → AgentToolResult
  ROUTE SpellFault → AgentToolResult
  ROUTE AgentResponse → ChatEfferentDraft
```

### Inner Covenant (Encapsulated)

```
COVENANT inner (owned by SpellcastingDaemon)
  CONNECT boundary    -- IScrivener<SpellEntry>
  CONNECT filesystem  -- IScrivener<FileSystemEntry>
  CONNECT compute     -- IScrivener<ComputeEntry>
  
  -- Dispatch
  ROUTE FileReadSpell → FileRead
  ROUTE FileWriteSpell → FileWrite
  ROUTE ShellExecSpell → ShellExec
  
  -- Gather
  ROUTE FileContent → SpellResult
  ROUTE FileFault → SpellFault
  ROUTE ShellOutput → SpellResult
  ROUTE ShellFault → SpellFault
```

### Runtime Topology

```
┌─────────────────────────────────────────────────────────────────────┐
│ DaemonScope (outer)                                                 │
│                                                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────────┐ │
│  │ ChatDaemon   │  │ AgentDaemon  │  │ SpellcastingDaemon         │ │
│  │              │  │              │  │ (CompositeDaemon)          │ │
│  │ tails:       │  │ tails:       │  │                            │ │
│  │  ChatEntry   │  │  AgentEntry  │  │  ┌──────────────────────┐  │ │
│  └──────────────┘  └──────────────┘  │  │ Inner scope          │  │ │
│                                      │  │                      │  │ │
│  CovenantAdherentDaemon (outer)      │  │  FileSystemDaemon    │  │ │
│  pumps: Chat↔Agent↔Spell routes      │  │  ComputeDaemon       │  │ │
│                                      │  │                      │  │ │
│                                      │  │  Inner pumps:        │  │ │
│                                      │  │   Spell↔FS↔Compute   │  │ │
│                                      │  └──────────────────────┘  │ │
│                                      └────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

## API Sketch

### Declaration

```csharp
// Extension method returns composite manifest
public static CompositeBranchManifest UseSpellcasting(this CovenServiceBuilder coven)
{
    return coven.CompositeManifest<SpellEntry, SpellcastingDaemon>(
        name: "Spellcasting",
        produces: new HashSet<Type> { typeof(SpellResult), typeof(SpellFault) },
        consumes: new HashSet<Type> { typeof(FileReadSpell), typeof(ShellExecSpell) },
        inner =>
        {
            // Declare inner branches
            BranchManifest fs = inner.Branch("FileSystem", typeof(FileSystemEntry),
                produces: new HashSet<Type> { typeof(FileContent), typeof(FileWritten), typeof(FileFault) },
                consumes: new HashSet<Type> { typeof(FileRead), typeof(FileWrite) },
                daemons: [typeof(FileSystemDaemon)]);
            
            BranchManifest compute = inner.Branch("Compute", typeof(ComputeEntry),
                produces: new HashSet<Type> { typeof(ShellOutput), typeof(ShellFault) },
                consumes: new HashSet<Type> { typeof(ShellExec) },
                daemons: [typeof(ComputeDaemon)]);
            
            // Connect boundary and inner branches
            inner.ConnectBoundary();
            inner.Connect(fs);
            inner.Connect(compute);
            
            // Define routes
            inner.Routes(c =>
            {
                // Dispatch: boundary → inner
                c.Route<FileReadSpell, FileRead>(...);
                c.Route<FileWriteSpell, FileWrite>(...);
                c.Route<ShellExecSpell, ShellExec>(...);
                
                // Gather: inner → boundary
                c.Route<FileContent, SpellResult>(...);
                c.Route<FileFault, SpellFault>(...);
                c.Route<ShellOutput, SpellResult>(...);
                c.Route<ShellFault, SpellFault>(...);
            });
        });
}
```

### Connection

```csharp
builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseDiscordChat(config);
    BranchManifest agents = coven.UseOpenAIAgents(agentConfig);
    CompositeBranchManifest spellcasting = coven.UseSpellcasting();
    
    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Connect(spellcasting)  // Looks like any other branch
        .Routes(c =>
        {
            c.Route<AgentToolCall, SpellInvocation>(...);
            c.Route<SpellResult, AgentToolResult>(...);
            // ...
        });
});
```

---

## Substrate Configuration

Composites expose their inner substrates as configurable surfaces. Users select which substrates to enable and how to configure them:

```
COMPOSITE Spellcasting
  SUBSTRATE FileSystem
    UseLocal(root)
    UseSftp(host, root)
    UseMock(files)
  
  SUBSTRATE Compute
    UseLocal(allowList?)
    UseMock(responses)
    UseContainer(image)
```

Each `Use*` registers a leaf daemon for that substrate.

### Computed Boundary Manifest

At `BuildCoven()`, the composite computes its boundary manifest from enabled substrates:

```
boundary.consumes = union of spell types from enabled substrates
boundary.produces = SpellResult, SpellFault (always)
```

| Configured | Contributes to `consumes` |
|------------|---------------------------|
| FileSystem has leaf | `FileReadSpell`, `FileWriteSpell`, `FileDeleteSpell`, ... |
| Compute has leaf | `ShellExecSpell` |
| Neither | (empty — composite does nothing) |

### Auto-Fault for Disabled Substrates

Disabled substrates auto-fault rather than dead-letter:

- All spell types remain in `consumes` (boundary surface is stable)
- Inner covenant routes disabled spell types directly to `SpellFault`
- No dead letters—every entry has a destination

This preserves the no-dead-letter invariant while allowing partial configuration:

```
BUILD-COVEN
  spellcasting = UseSpellcasting()
  spellcasting.FileSystem.UseLocal(root: "/workspace")
  -- spellcasting.consumes includes ALL spell types
  -- ShellExecSpell routes to auto-fault (Compute not configured)
```

### Multiple Leaves Per Substrate

A substrate can have multiple leaves with scoped dispatch:

```
spellcasting.FileSystem.UseLocal(root: "/workspace")
spellcasting.FileSystem.UseSftp(host: "remote", root: "/data")
```

Both leaves tail the same `FileSystemEntry` journal. Leaves filter by path scope—`LocalFSDaemon` handles `/workspace/*`, `SftpFSDaemon` handles `/data/*`.

From validation's perspective: FileSystem has ≥1 leaf, so its spell types are valid. Scoped dispatch is a leaf concern, not a configuration concern.

---

## Design Decisions

### Build-Time Validation (Critical)

Inner covenants exist to provide **the same compile-time guarantees as outer covenants**. When `BuildCoven()` runs, validation must prove:

1. **Boundary produces are reachable**: Every type in the composite's `produces` must be the target of some inner route. If `SpellResult` is declared as produced, there must be `ROUTE SomeInnerType → SpellResult`.

2. **Boundary consumes are dispatched**: Every type in the composite's `consumes` must be the source of some inner route. If `SpellInvocation` (polymorphic) is consumed, each concrete subtype (`FileReadSpell`, etc.) must have a route.

3. **No dead letters**: Every produced type in every inner manifest must route somewhere—either to another inner branch, back to the boundary, or be explicitly terminal. Silence is not an option.

4. **Auto-fault completeness**: If a substrate is disabled, its spell types must auto-route to `SpellFault`. The boundary's `consumes` surface is stable regardless of configuration.

**Failure mode**: `CovenantValidationException` at build time with actionable error messages. The application does not start.

### Scrivener Lifecycle

Inner scriveners are `InMemoryScrivener<T>` by default, created by `CompositeDaemon` at startup. Future work may allow configurable scrivener factories for debugging/persistence, but the default is in-memory and ephemeral.

### Inner Daemon DI

Inner daemons receive a **child `IServiceProvider` scope** that:
- Inherits outer services (logging, configuration, etc.)
- Registers inner scriveners as `IScrivener<TInner>`
- Isolates inner infrastructure from outer DI

This gives inner daemons full DI capabilities while keeping inner scriveners invisible to outer code.

### Runtime Error Propagation

**Fail-fast**: If an inner daemon faults during operation, the `CompositeDaemon` faults. The outer scope sees a daemon failure and can react (typically by failing the ritual).

Rationale: The inner sub-graph is an implementation detail. If it breaks, the composite is broken. Sophisticated recovery (restart with backoff, circuit breakers) is future work—see [Daemon Magistrate](daemon-magistrate.md).

### Observability

Out of scope for MVP. Inner journals are opaque to outer code. Future work may add:
- Debug scrivener decorator that logs entries
- Metagraph queries for inner structure introspection
- Diagnostic endpoints for inner journal state

---

## Checklist

**Types and Builders**
- [ ] `CompositeBranchManifest` type (boundary + inner structure)
- [ ] `InnerCovenantBuilder` for declaring inner branches and routes
- [ ] `CompositeDaemon<TBoundary>` base class

**Build-Time Validation (Critical Path)**
- [ ] Inner covenant coverage validation (every produces has route/terminal)
- [ ] Inner covenant uniqueness validation (one route per source)
- [ ] Inner covenant consumer satisfaction (every consumes has producer)
- [ ] Boundary coherence: `produces` reachable from inner routes
- [ ] Boundary coherence: `consumes` dispatched to inner routes
- [ ] Auto-fault routes for unconfigured substrates
- [ ] `CovenantValidationException` with actionable messages

**Runtime Infrastructure**
- [ ] Integration with `CovenantBuilder.Connect()` for composite manifests
- [ ] Inner scrivener creation (`InMemoryScrivener` by default)
- [ ] Child `IServiceProvider` scope for inner daemons
- [ ] Inner daemon instantiation and lifecycle
- [ ] Inner pump execution
- [ ] Fail-fast on inner daemon fault

**Configuration**
- [ ] Substrate configuration API (`UseLocal`, `UseMock`, etc.)
- [ ] `SpellFault` includes reason ("substrate not configured")

**Tests**
- [ ] Test: composite with two inner branches, full round-trip
- [ ] Test: configure FileSystem only, route FileReadSpell → SpellResult
- [ ] Test: configure FileSystem only, route ShellExecSpell → SpellFault (auto-fault)
- [ ] Test: missing inner route → build-time validation error
- [ ] Test: boundary produces unreachable → build-time validation error
