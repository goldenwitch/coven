# Metagraph

> **Status**: Draft  
> **Created**: 2026-01-30

---

## Summary

The **metagraph** is a build-time registry where branches publish metadata for discovery. It enables optional introspection—consumers query it if they want; the core system works without it.

---

## Vocabulary

| Term | Definition |
|------|------------|
| **Metagraph** | Build-time registry where branches publish metadata for discovery |
| **Capability** | Metadata describing what a branch can do (spell types, schemas, descriptions) |

---

## Motivation

Branches need to advertise their capabilities without coupling consumers to implementation details:

- Spellcasting publishes available spell types so agent leaves can format them as tool definitions
- Agent leaves query capabilities at build time, not runtime
- Disabled substrates don't advertise (or advertise as `enabled: false`)

The alternative—hardcoding tool definitions in each consumer—works but doesn't scale. The metagraph provides optional discovery while keeping direct usage viable.

---

## Design

### Direct Usage (No Metagraph)

If you know the spell types, use them directly:

```
-- This works fine, no discovery needed
WRITE FileReadSpell { correlation-id: "abc", path: "/foo" } to SpellEntry
-- Eventually receive SpellResult or SpellFault with correlation-id: "abc"
```

An agent that hardcodes its tools doesn't need the metagraph at all.

### Publishing Capabilities

Branches publish capability metadata at `BuildCoven` time:

```
METAGRAPH (populated at BuildCoven)
  spellcasting:
    capabilities:
      - type: FileReadSpell
        schema: { path: string }
        description: "Read contents of a file"
        result-type: FileContent
        fault-type: FileFault
        
      - type: FileWriteSpell
        schema: { path: string, content: bytes }
        description: "Write contents to a file"
        result-type: FileWritten
        fault-type: FileFault
        
      - type: ShellExecSpell
        schema: { command: string, working_dir?: string }
        description: "Execute a shell command"
        result-type: ShellOutput
        fault-type: ShellFault
        enabled: false  -- Compute not configured
```

This is just data. Branches publish it; nobody is required to read it.

### Querying Capabilities

Consumers query the metagraph at build time:

```
BUILD-TIME (UseOpenAIAgents)
  -- Option A: Hardcode tools (no metagraph)
  leaf.Tools = [ReadFileTool, WriteFileTool]
  
  -- Option B: Discover from metagraph
  spell-caps = metagraph.Query<SpellcastingCapabilities>()
  leaf.Tools = spell-caps
    .Where(c => c.Enabled)
    .Select(c => new ToolDefinition {
        Name = to_snake_case(c.Type.Name),
        Description = c.Description,
        Parameters = c.Schema,
        SpellType = c.Type
    })
```

The consumer receives capability data at **construction**, not at runtime.

### Capability Record

Each capability includes type information and schema:

```
RECORD SpellCapability
  spell-type: Type
  schema: JsonSchema
  description: string
  result-type: Type
  fault-type: Type
  enabled: bool
```

### Capabilities Reflect Configuration

Only **enabled substrates** advertise capabilities as available. Disabled substrates appear with `enabled: false` (or are omitted entirely). The metagraph reflects the actual system state.

---

## Build-Time Flow

```
BuildCoven
  │
  ├─ UseSpellcasting(cfg)
  │    ├─ cfg.FileSystem.UseLocal(root)  → FileSystem enabled
  │    └─ (Compute not configured)       → Compute disabled
  │
  ├─ Spellcasting publishes to metagraph:
  │    └─ capabilities: [FileRead✓, FileWrite✓, FileDelete✓, ShellExec✗]
  │
  ├─ UseOpenAIAgents(cfg)
  │    └─ queries metagraph, configures tools from enabled capabilities
  │
  └─ Covenant wiring (uses manifest types, not metagraph)
```

The metagraph is **orthogonal to covenant routing**. Covenants use `BranchManifest` types; the metagraph is for optional discovery by leaves that want dynamic tool configuration.

---

## Scope

**In scope:**
- Metagraph registry populated at build time
- Query interface for capability discovery
- SpellCapability record structure

**Out of scope:**
- Runtime metagraph updates
- Cross-coven metagraph federation
- Metagraph persistence

---

## Checklist

- [ ] `IMetagraph` interface with `Publish<T>` and `Query<T>`
- [ ] `SpellCapability` record
- [ ] Spellcasting publishes capabilities at build time
- [ ] Agent leaves can query and format capabilities
- [ ] Disabled substrates excluded or marked `enabled: false`
- [ ] Integration test: metagraph discovery flow
