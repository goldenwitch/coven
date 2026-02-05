# Spellcasting Branch

> **Status**: Draft — Needs Revision  
> **Created**: 2026-01-25

**⚠️ Note**: This proposal uses the deleted composite/inner covenant model. The architecture should be revised to use the **flat covenant model** per [unified-covenant-builder.md](unified-covenant-builder.md). Spellcasting becomes a regular branch that registers its daemons and scriveners directly; FileSystem and Compute connect as peer branches in one flat graph.

---

## Summary

The **Spellcasting Branch** abstracts tool invocation as "fire a function, get a result."

External branches write polymorphic `SpellInvocation` entries and receive `SpellResult` or `SpellFault`. ~~Internally, Spellcasting is a **composite branch** with an inner covenant that routes spell types to substrate-specific leaves (FileSystem, Compute). The outer world sees only the boundary; internal dispatch is opaque.~~

---

## Vocabulary

Coven terms used in this proposal:

| Term | Definition |
|------|------------|
| **Branch** | Abstraction layer with typed journals + services (Chat, Agents, Spellcasting) |
| **Leaf** | Integration translating a branch to an external system (Discord, OpenAI, FileSystem) |
| **Scrivener** | Append-only typed journal (`IScrivener<T>`) |
| **Daemon** | Long-running service with lifecycle (Start/Shutdown/Status) |
| **Covenant** | Declarative routes between branch journals |
| **Composite Branch** | A branch containing inner branches with its own internal covenant |
| **Substrate** | An inner leaf within a composite branch (e.g., FileSystem within Spellcasting) |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      OUTER COVENANT                             │
│                                                                 │
│   Agents                         Spellcasting                   │
│   ┌─────────┐                   ┌─────────────────────────────┐ │
│   │         │ AgentToolCall     │                             │ │
│   │         │──────────────────▶│  SpellcastingDaemon         │ │
│   │         │                   │  (CompositeDaemon)          │ │
│   │         │                   │                             │ │
│   │         │                   │  ┌─────────────────────────┐│ │
│   │         │◀──────────────────│  │ INNER COVENANT         ││ │
│   │         │ SpellResult       │  │                         ││ │
│   └─────────┘                   │  │ FileSystem    Compute   ││ │
│                                 │  │ ┌────────┐  ┌────────┐  ││ │
│                                 │  │ │        │  │        │  ││ │
│                                 │  │ └────────┘  └────────┘  ││ │
│                                 │  └─────────────────────────┘│ │
│                                 └─────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

The `SpellcastingDaemon` is a `CompositeDaemon` that:
- Owns inner scriveners (`FileSystemEntry`, `ComputeEntry`)
- Runs inner covenant routing pumps
- Manages child daemons (`FileSystemDaemon`, `ComputeDaemon`)

---

## Composite Manifest

```
COMPOSITE-MANIFEST Spellcasting

  BOUNDARY
    journal: SpellEntry
    produces: SpellResult, SpellFault
    consumes: SpellInvocation

  INNER-BRANCHES
    FileSystem
      journal: FileSystemEntry
      produces: FileContent, FileWritten, FileFault
      consumes: FileRead, FileWrite, FileDelete
      daemons: FileSystemDaemon
    
    Compute
      journal: ComputeEntry
      produces: ShellOutput, ShellFault
      consumes: ShellExec
      daemons: ComputeDaemon

  INNER-COVENANT
    -- Dispatch: boundary → substrates
    ROUTE FileReadSpell → FileRead
    ROUTE FileWriteSpell → FileWrite
    ROUTE FileDeleteSpell → FileDelete
    ROUTE ShellExecSpell → ShellExec
    
    -- Gather: substrates → boundary
    ROUTE FileContent → SpellResult
    ROUTE FileWritten → SpellResult
    ROUTE FileFault → SpellFault
    ROUTE ShellOutput → SpellResult
    ROUTE ShellFault → SpellFault
```

**Dispatch is type-based.** The polymorphic `SpellInvocation` hierarchy enables standard covenant routing—no special dispatch logic needed.

---

## Boundary Entries (SpellEntry)

```
ENTRY SpellEntry (polymorphic, discriminator: $type)

  -- Invocations (consumed from outer covenant)
  SpellInvocation (abstract)
    correlation-id: guid
  
  FileReadSpell : SpellInvocation
    path: string
  
  FileWriteSpell : SpellInvocation
    path: string
    content: bytes
  
  FileDeleteSpell : SpellInvocation
    path: string
  
  ShellExecSpell : SpellInvocation
    command: string
    working-dir: string?
  
  -- Results (produced for outer covenant)
  SpellResult
    correlation-id: guid
    payload: object
  
  SpellFault
    correlation-id: guid
    error: string
```

All entries carry `correlation-id` for request/response matching across journal boundaries.

---

## Substrate Integration

Substrates (FileSystem, Compute) are detailed in separate proposals:
- [FileSystem Sub-branch](filesystem-branch.md)
- [Compute Sub-branch](compute-branch.md)

Each substrate declares the spell types it handles:

```
SUBSTRATE-MANIFEST FileSystem
  spell-types: [FileReadSpell, FileWriteSpell, FileDeleteSpell]
  inner-journal: FileSystemEntry
  daemon: FileSystemDaemon
  
  transforms:
    FileReadSpell → FileRead
    FileWriteSpell → FileWrite
    FileDeleteSpell → FileDelete
  
  result-transforms:
    FileContent → SpellResult
    FileWritten → SpellResult
    FileFault → SpellFault
```

### Build-Time Aggregation

When building the composite:

```
coven.UseSpellcasting(spellcasting =>
{
    spellcasting.FileSystem.UseLocal(root: "/workspace");
    // Compute not configured
});
```

Spellcasting:
1. Collects spell types from enabled substrates
2. Generates inner covenant routes for those types
3. Generates auto-fault routes for disabled substrate types
4. Publishes capabilities to metagraph (see [Metagraph](metagraph.md))

---

## Related Proposals

| Proposal | Relationship |
|----------|--------------|
| [FileSystem Sub-branch](filesystem-branch.md) | FileSystem substrate details |
| [Compute Sub-branch](compute-branch.md) | Compute substrate details |
| [Metagraph](metagraph.md) | Capability discovery mechanism |
| [Agent-Spellcasting Integration](agent-spellcasting-integration.md) | How agents consume Spellcasting |

---

## Checklist

- [ ] `SpellEntry` hierarchy with polymorphic invocation types
- [ ] `SpellResult` and `SpellFault` boundary entries
- [ ] `SubstrateManifest` with spell types and transforms
- [ ] `UseSpellcasting()` returning `CompositeBranchManifest`
- [ ] Inner covenant wiring (dispatch + gather routes)
- [ ] Auto-fault routes for disabled substrates
- [ ] Integration test: spell → substrate → result round-trip
