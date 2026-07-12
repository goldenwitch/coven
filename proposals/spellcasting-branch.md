# Spellcasting

> **Status**: Revised  
> **Created**: 2026-01-25  
> **Revised**: 2026-02-09

---

## Summary

**Spellcasting** is Coven's pattern for tool invocation. There is no `Coven.Spellcasting` package — tool definitions live in `Coven.Agents` (`ToolDefinition`), and each tool capability is modeled as an ordinary branch.

Each tool capability (FileSystem, Compute, ImageGen) is its own **branch** — defining entry types the same way Chat defines `ChatAfferent`/`ChatEfferent`. Each branch has **leaves** that translate those entries to concrete backends (POSIX, Windows, DALL-E). **Companion libraries** bridge agents to tool branches, keeping types scoped to exactly where they're needed.

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        FLAT COVENANT                             │
│                                                                  │
│   Chat           Agents          FileSystem        Compute       │
│   ┌────────┐    ┌──────────┐    ┌────────────┐   ┌──────────┐   │
│   │Discord │    │ OpenAI   │    │ POSIX      │   │ POSIX    │   │
│   │  leaf  │    │  leaf    │    │  leaf      │   │  leaf    │   │
│   └────────┘    └──────────┘    └────────────┘   └──────────┘   │
│                                                                  │
│   Branches define entry types. Leaves talk to backends.          │
│   Covenant routes between branches.                              │
└──────────────────────────────────────────────────────────────────┘
```

---

## Type Containment

Types are surfaced only in the exact package that needs them:

- **Branch packages** (`Coven.FileSystem`) own entry types. They don't know about agents.
- **Leaf packages** (`Coven.FileSystem.Posix`) own daemons that back a branch. They don't know about agents.
- **Agent packages** (`Coven.Agents.OpenAI`) own agent entry types. They don't know about tool branches.
- **Companion libraries** (`Coven.Agents.FileSystem`) reference agent and branch packages, providing the transmuters.

If you don't use a branch, you don't have its types. If you don't bridge an agent to a branch, neither knows the other exists.

---

## Package Structure

Three tiers per tool capability:

| Tier | Example Package | Contains | References |
|------|----------------|----------|------------|
| **Branch** | `Coven.FileSystem` | Entry types (`FileRead`, `FileContent`, etc.), `BranchManifest` | `Coven.Core` |
| **Leaf** | `Coven.FileSystem.Posix` | Daemon (`PosixFileSystemDaemon`), `UsePosixFileSystem()` | Branch package |
| **Companion** | `Coven.Agents.FileSystem` | Tool definitions, transmuters | `Coven.Agents` + branch package |

The companion bridges agents to the branch, not to a specific leaf. `Coven.Agents.FileSystem` provides transmuters from `AgentToolCall` → `FileRead` regardless of whether POSIX or Windows is backing the FileSystem branch.

---

## Tool Definitions

`ToolDefinition` (in `Coven.Agents`) carries tool name + description + JSON input schema. Companion libraries register `ToolDefinition` instances in DI to describe their tools; agent leaves consume them to format LLM tool registrations. Schema generation from CLR types (`SchemaGen`) is future work.

---

## Companion Libraries

A companion library bridges agents to a tool branch. It provides:

1. **Tool definitions** — `ToolDefinition[]` describing available operations
2. **Transmuters** — route `AgentToolCall` → branch efferent entries, and branch afferent entries → `AgentToolResult`

```
Coven.Agents.FileSystem
  ├── FileSystemTools              → ToolDefinition[] for FileRead, FileWrite, etc.
  ├── AgentToolCallToFileRead      → transmuter
  ├── AgentToolCallToFileWrite     → transmuter
  ├── FileContentToAgentToolResult → transmuter
  └── FileFailureToAgentToolFailure → transmuter
```

Routes carry predicates (`Route<TSource, TTarget, TTransmuter>(shouldRoute)`), so multiple routes from `AgentToolCall` coexist cleanly — each route declares exactly which tool calls it handles, and transmuters stay pure.

---

## Covenant Routing

Users wire routes at design time. Companion libraries provide the transmuters:

```csharp
services.BuildCoven(coven =>
{
    var chat = coven.UseDiscordChat(discordConfig);
    var agents = coven.UseOpenAIAgents(agentConfig);
    var filesystem = coven.UseFileSystem(fs => fs.UsePosix(root: "/workspace"));

    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Connect(filesystem)
        .Routes(c =>
        {
            // Chat ↔ Agents
            c.Route<ChatAfferent, AgentPrompt>(/* ... */);
            c.Route<AgentResponse, ChatEfferentDraft>(/* ... */);

            // Agents ↔ FileSystem (from Coven.Agents.FileSystem companion)
            c.Route<AgentToolCall, FileRead, AgentToolCallToFileRead>();
            c.Route<AgentToolCall, FileWrite, AgentToolCallToFileWrite>();
            c.Route<FileContent, AgentToolResult, FileContentToAgentToolResult>();
            c.Route<FileFailure, AgentToolFailure, FileFailureToAgentToolFailure>();

            c.Terminal<AgentThought>();
        });
});
```

---

## Related Proposals

| Proposal | Relationship |
|----------|--------------|
| [FileSystem Branch](filesystem-branch.md) | FileSystem entry types and leaves |
| [Compute Branch](compute-branch.md) | Compute entry types and leaves |
| [ImageGen Branch](imagegen-substrate.md) | ImageGen entry types and leaves |
| [Agent Tool Calls](agent-spellcasting-integration.md) | Agent entry types and tool call flow |

---

## Checklist

- [x] Delete `ISpellContract`, `ISpell`, `Spellbook`, `SpellbookBuilder` (replaced by companion pattern)
- [x] `ToolDefinition` in `Coven.Agents` (the `Coven.Spellcasting` utility package was deleted instead of slimmed)
- [x] `Coven.FileSystem` branch package with entry types
- [x] `Coven.FileSystem.Posix` leaf with daemon, `UsePosixFileSystem()` (read-only so far)
- [ ] `Coven.Compute` branch package with entry types
- [ ] `Coven.Compute.Posix` leaf with daemon, `UseCompute().UsePosix()`
- [ ] `Coven.ImageGen` branch package with entry types
- [ ] `Coven.ImageGen.Dalle` leaf with daemon, `UseImageGen().UseDalle()`
- [x] `Coven.Agents.FileSystem` companion with tool definitions and transmuters
- [ ] `Coven.Agents.Compute` companion with tool definitions and transmuters
- [ ] `Coven.Agents.ImageGen` companion with tool definitions and transmuters
- [x] Integration test: agent → companion → branch → leaf → branch → companion → agent round-trip
