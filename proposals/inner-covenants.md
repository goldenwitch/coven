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

Inner covenants get the same validation as outer covenants:

1. **Coverage**: Every type in any inner manifest's `Consumes` must have a `Route` or `Terminal`
2. **Uniqueness**: Each source type has at most one route
3. **Production**: Every type in any inner manifest's `Produces` must have a route producing it
4. **Boundary coherence**: Inner routes that cross the boundary must connect to declared `produces`/`consumes`

Validation runs at `BuildCoven()` time, before any daemon starts.

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

See [Spellcasting Branch](tooling-branch.md) for details on metagraph usage.

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
    return coven.CompositeManifest(
        name: "Spellcasting",
        boundaryJournal: typeof(SpellEntry),
        produces: [typeof(SpellResult), typeof(SpellFault)],
        consumes: [typeof(SpellInvocation)],
        inner: inner =>
        {
            // Declare inner branches
            BranchManifest fs = inner.Branch("FileSystem", typeof(FileSystemEntry),
                produces: [typeof(FileContent), typeof(FileWritten), typeof(FileFault)],
                consumes: [typeof(FileRead), typeof(FileWrite)],
                daemons: [typeof(FileSystemDaemon)]);
            
            BranchManifest compute = inner.Branch("Compute", typeof(ComputeEntry),
                produces: [typeof(ShellOutput), typeof(ShellFault)],
                consumes: [typeof(ShellExec)],
                daemons: [typeof(ComputeDaemon)]);
            
            // Wire inner covenant
            inner.Covenant()
                .Connect(fs)
                .Connect(compute)
                .Routes(c =>
                {
                    // Dispatch
                    c.Route<FileReadSpell, FileRead>(...);
                    c.Route<FileWriteSpell, FileWrite>(...);
                    c.Route<ShellExecSpell, ShellExec>(...);
                    
                    // Gather
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

## Open Questions

1. **Scrivener lifecycle**: Inner scriveners are created by `CompositeDaemon`. Should they be `InMemoryScrivener` by default, or configurable (e.g., for persistence/debugging)?

2. **Inner daemon DI**: Inner daemons need services (logging, config). Do they share the outer `IServiceProvider`, or get a child scope?

3. **Error propagation**: If an inner daemon faults, how does that surface? Options:
   - `CompositeDaemon` faults (kills outer scope)
   - Fault entry written to boundary journal
   - Inner daemon restarts with backoff

4. **Observability**: How do we expose inner journal contents for debugging without breaking encapsulation?

---

## Checklist

- [ ] `CompositeBranchManifest` type
- [ ] `InnerCovenantBuilder` for declaring inner branches and routes
- [ ] `CompositeDaemon<TBoundary>` base class
- [ ] Validation: inner covenant coverage and boundary coherence
- [ ] Integration with `CovenantBuilder.Connect()` for composite manifests
- [ ] Inner scrivener creation and lifecycle
- [ ] Inner daemon instantiation and lifecycle
- [ ] Substrate configuration API (`UseLocal`, `UseMock`, etc.)
- [ ] Auto-fault routes for unconfigured substrates
- [ ] Build-time validation: enabled paths complete
- [ ] SpellFault includes reason ("substrate not configured")
- [ ] Test: composite with two inner branches, full round-trip
- [ ] Test: configure FileSystem only, route FileReadSpell → SpellResult
- [ ] Test: configure FileSystem only, route ShellExecSpell → SpellFault
