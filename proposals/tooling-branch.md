# Spellcasting and Target Branches

> **Status**: Draft
> **Created**: 2026-01-25
> **Depends on**: [filesystem-branch.md](filesystem-branch.md), [compute-branch.md](compute-branch.md)

---

## Summary

Enable agents to dispatch spells that perform real actions by introducing a **two-level branch architecture**:

1. **Spellcasting Branch** — Abstracts tool invocation/result flow (agents write invocations, receive results)
2. **Target Branches** — Abstract the substrates spells operate on (FileSystem, Compute, etc.), each with concrete leaves

This follows Coven's Branch/Leaf model: branches define abstract entry types, leaves translate to concrete backends. Spells become covenants that route between the Spellcasting branch and one or more target branches.

---

## Problem

Today, Coven agents can think and chat but cannot act. The Spellcasting package defines typed tools with schema generation, but nothing wires those tools to:

1. Agent conversations (parsing tool calls, returning results)
2. Actual execution substrates (file systems, shells, APIs)

We need infrastructure that:
- Flows tool calls through journals (auditable, replayable)
- Abstracts **what** the tool does (Spellcasting branch)
- Abstracts **where** it executes (Target branches with swappable leaves)

---

## Design

### Two-Hop Architecture

```
                         Agents Branch
                              │
                              │ AgentToolCall / AgentToolCallResult
                              ▼
                    ┌─────────────────────┐
                    │  Spellcasting Branch │
                    │  (spell invocation)  │
                    └─────────────────────┘
                              │
            ┌─────────────────┼─────────────────┐
            │                 │                 │
            ▼                 ▼                 ▼
    ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
    │  FileSystem   │ │    Compute    │ │    [Future]   │
    │    Branch     │ │    Branch     │ │   Git, HTTP,  │
    └───────────────┘ └───────────────┘ │   Database... │
            │                 │         └───────────────┘
       ┌────┴────┐       ┌────┴────┐
       │         │       │         │
       ▼         ▼       ▼         ▼
    PosixFS  WindowsFS PosixShell WindowsShell
```

Each level is a proper Coven branch:
- **Spellcasting** abstracts "do this thing" — entry types for invocation and results
- **Target branches** abstract the substrate — entry types for operations on that substrate
- **Leaves** implement concrete backends — translate abstract entries to real I/O

### Spellcasting Branch

The top-level branch that agents interact with.

**Entry Types:**

| Entry | Direction | Purpose |
|-------|-----------|---------|
| `SpellInvocation` | Afferent | Request to cast a spell (name, args, correlation ID) |
| `SpellResult` | Efferent | Successful completion (correlation ID, result) |
| `SpellFault` | Efferent | Execution failure (correlation ID, error details) |

**No leaves.** The Spellcasting branch doesn't talk to external systems directly. Instead, spells are **covenants** that route between Spellcasting and target branches.

**Daemon:** `SpellcastingDaemon` tails `SpellInvocation`, looks up the spell, executes it. The spell implementation writes to target branches and reads their results.

### FileSystem Branch

Abstracts file operations. Spells that manipulate files route through this branch.

**Entry Types:**

| Entry | Direction | Purpose |
|-------|-----------|---------|
| `FileRead` | Efferent | Request to read file content |
| `FileContent` | Afferent | File content response |
| `FileWrite` | Efferent | Request to write content to path |
| `FileWritten` | Afferent | Write confirmation |
| `FileList` | Efferent | Request directory listing |
| `FileListing` | Afferent | Directory entries response |
| `FileDelete` | Efferent | Request to delete file/directory |
| `FileDeleted` | Afferent | Delete confirmation |
| `FileStat` | Efferent | Request file metadata |
| `FileMetadata` | Afferent | Metadata response (size, modified, permissions) |
| `FileFault` | Afferent | Operation failed (not found, permission denied, etc.) |

**Leaves:**

| Leaf | Implementation |
|------|---------------|
| `LocalFileSystem` | Direct file I/O on local disk |
| `SftpFileSystem` | SFTP connection to remote host |
| `S3FileSystem` | AWS S3 bucket operations |
| `MockFileSystem` | In-memory file system for testing |

Each leaf daemon tails efferent entries, performs the operation, writes afferent results.

**Path semantics:** All paths are branch-relative. The leaf translates to absolute paths. A `LocalFileSystem` might root at `/workspace`. An `S3FileSystem` prefixes with bucket and key prefix.

### Compute Branch

Abstracts command execution. Spells that run processes route through this branch.

**Entry Types:**

| Entry | Direction | Purpose |
|-------|-----------|---------|
| `ShellExec` | Efferent | Request to execute command |
| `ShellOutput` | Afferent | Command completed (exit code, stdout, stderr) |
| `ShellOutputChunk` | Afferent | Streaming output chunk (for long-running commands) |
| `ShellFault` | Afferent | Execution failed (command not found, timeout, etc.) |

**Leaves:**

| Leaf | Implementation |
|------|---------------|
| `LocalShell` | `Process.Start` on local machine |
| `SshShell` | SSH connection to remote host |
| `ContainerShell` | Docker/Podman exec in container |
| `MockShell` | Scripted responses for testing |

**Streaming:** Long-running commands emit `ShellOutputChunk` entries. A windowing policy aggregates chunks into final `ShellOutput` when the process exits.

**Exit codes:** Non-zero exit is not automatically a fault. `ShellOutput` includes exit code; the spell decides whether it's an error.

### Spells as Covenants

A spell is a mini-covenant that routes between Spellcasting and target branches:

```
ReadFileSpell
├── Receives: SpellInvocation (name="read_file", args={path: "..."})
├── Writes:   FileRead to FileSystem branch
├── Awaits:   FileContent from FileSystem branch
└── Writes:   SpellResult to Spellcasting branch

RunCommandSpell
├── Receives: SpellInvocation (name="run_command", args={cmd, args})
├── Writes:   ShellExec to Compute branch
├── Awaits:   ShellOutput from Compute branch
└── Writes:   SpellResult to Spellcasting branch
```

**Compound spells** can orchestrate multiple target branches:

```
EditFileSpell
├── Receives: SpellInvocation (name="edit_file", args={path, edits})
├── Writes:   FileRead to FileSystem branch
├── Awaits:   FileContent
├── (apply edits in memory)
├── Writes:   FileWrite to FileSystem branch
├── Awaits:   FileWritten
└── Writes:   SpellResult
```

### Execution Flow

```
Agent Journal           Spellcasting           FileSystem Branch
     │                      │                        │
     │  AgentToolCall       │                        │
     │─────────────────────►│                        │
     │                      │                        │
     │               SpellInvocation                 │
     │                 (daemon receives)             │
     │                      │                        │
     │                      │  FileRead              │
     │                      │───────────────────────►│
     │                      │                        │
     │                      │                        │  (LocalFS leaf
     │                      │                        │   reads disk)
     │                      │                        │
     │                      │  FileContent           │
     │                      │◄───────────────────────│
     │                      │                        │
     │               SpellResult                     │
     │◄─────────────────────│                        │
     │  AgentToolCallResult │                        │
```

### Agent Integration

The Agents branch adds entry types for tool calls:

| Entry | Purpose |
|-------|---------|
| `AgentToolCall` | Agent requests tool execution |
| `AgentToolCallResult` | Result returned to agent conversation |

**Covenant routes:**

```
Route: AgentToolCall → SpellInvocation
Route: SpellResult → AgentToolCallResult
Route: SpellFault → AgentToolCallResult (with error formatting)
```

**Provider changes:** Each agent leaf (OpenAI, Gemini) must:
1. Register spellbook definitions as tools in API requests
2. Parse tool call responses into `AgentToolCall` entries
3. Include `AgentToolCallResult` in subsequent conversation turns

### Branch Composition

Applications compose branches based on their needs:

**Local development agent:**
```
Agents (OpenAI)
  └── Spellcasting
        ├── FileSystem (LocalFS leaf)
        └── Compute (LocalShell leaf)
```

**Remote server agent:**
```
Agents (OpenAI)
  └── Spellcasting
        ├── FileSystem (SftpFS leaf)
        └── Compute (SshShell leaf)
```

**Sandboxed agent:**
```
Agents (OpenAI)
  └── Spellcasting
        ├── FileSystem (LocalFS leaf with path restrictions)
        └── Compute (ContainerShell leaf)
```

**Testing:**
```
Agents (Mock)
  └── Spellcasting
        ├── FileSystem (MockFS leaf)
        └── Compute (MockShell leaf)
```

### Security Model

Security is enforced at the **leaf level**, not the branch level:

| Concern | Where Enforced |
|---------|---------------|
| Path restrictions | FileSystem leaf configuration |
| Command allowlists | Compute leaf configuration |
| Network isolation | Container/SSH leaf boundaries |
| Timeouts | Leaf-level execution limits |

The branch abstraction doesn't know about security. The leaf implements whatever restrictions the application requires.

---

## Alternatives Considered

### Single "Envoy" Abstraction

The first draft proposed a monolithic `Envoy` that bundled FileSystem + Shell + Environment. This violates Coven's decomposition principles:

- Envoy isn't a branch (no journal, no entries)
- Can't swap file system independently from shell
- Testing requires mocking the entire Envoy
- No audit trail for individual operations

The two-hop model gives each concern its own journal, its own leaves, its own lifecycle.

### Spells Call Services Directly

Why not inject `IFileSystem` directly into spells instead of routing through branches?

- **No journaling** — Operations aren't recorded, can't replay or audit
- **Tight coupling** — Spells know about concrete services
- **No streaming** — Can't window long-running operations
- **No composition** — Can't route a spell's file operations to different backends per-deployment

The branch model keeps everything in journals.

### Tools in Agent Branch

Why separate Spellcasting from Agents?

- **Reuse** — Multiple agent types share one spellcasting infrastructure
- **Lifecycle** — Tool execution has different failure modes than agent conversations
- **Testing** — Can test spells without agent integration

---

## Future Target Branches

The two-hop model extends naturally:

| Branch | Purpose | Example Leaves |
|--------|---------|---------------|
| `Git` | Repository operations | Local, GitHub API, GitLab API |
| `Http` | Web requests | Standard HTTP client, cached, mocked |
| `Database` | Query execution | PostgreSQL, SQLite, mock |
| `Secrets` | Credential access | Env vars, Vault, AWS Secrets Manager |

Each follows the same pattern: abstract entries, concrete leaves, journal everything.

---

## Checklist

**Spellcasting Branch:**
- [ ] Define `SpellEntry` hierarchy (`SpellInvocation`, `SpellResult`, `SpellFault`)
- [ ] Implement `SpellcastingDaemon` that dispatches to spell implementations
- [ ] Define spell registration and lookup mechanism

**FileSystem Branch:**
- [ ] Define `FileSystemEntry` hierarchy (read, write, list, delete, stat, + responses)
- [ ] Implement `LocalFileSystem` leaf
- [ ] Implement `MockFileSystem` leaf for testing
- [ ] Add path restriction configuration for sandboxing

**Compute Branch:**
- [ ] Define `ComputeEntry` hierarchy (exec, output, chunk, fault)
- [ ] Implement `LocalShell` leaf
- [ ] Implement `MockShell` leaf for testing
- [ ] Add windowing for `ShellOutputChunk` → `ShellOutput`

**Agent Integration:**
- [ ] Define `AgentToolCall` and `AgentToolCallResult` entries
- [ ] Add covenant routes between agents and spellcasting
- [ ] Extend `OpenAIAgentSession` to register tools and parse `tool_calls`
- [ ] Extend `GeminiAgentSession` to register tools and parse `function_call`

**Built-in Spells:**
- [ ] `ReadFile` spell (Spellcasting → FileSystem)
- [ ] `WriteFile` spell
- [ ] `ListDirectory` spell
- [ ] `RunCommand` spell (Spellcasting → Compute)

**Future:**
- [ ] `SshShell` leaf
- [ ] `ContainerShell` leaf
- [ ] `SftpFileSystem` leaf
- [ ] Additional target branches (Git, Http, etc.)
