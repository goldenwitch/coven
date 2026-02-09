# Spellcasting

> **Status**: Revised  
> **Created**: 2026-01-25  
> **Revised**: 2026-02-09

---

## Summary

**Spellcasting** is Coven's pattern for tool invocation. The `Coven.Spellcasting` package is a slim **utility library** providing `SpellDefinition` and `SchemaGen`. It is not a branch.

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

- **Branch packages** (`Coven.Spellcasting.FileSystem`) own entry types. They don't know about agents.
- **Leaf packages** (`Coven.Spellcasting.FileSystem.Posix`) own daemons that back a branch. They don't know about agents.
- **Agent packages** (`Coven.Agents.OpenAI`) own agent entry types. They don't know about tool branches.
- **Companion libraries** (`Coven.Agents.FileSystem`) reference agent and branch packages, providing the transmuters.

If you don't use a branch, you don't have its types. If you don't bridge an agent to a branch, neither knows the other exists.

---

## Package Structure

Four tiers per tool capability:

| Tier | Example Package | Contains | References |
|------|----------------|----------|------------|
| **Branch** | `Coven.Spellcasting.FileSystem` | Entry types (`FileRead`, `FileContent`, etc.), `BranchManifest` | `Coven.Core` |
| **Leaf** | `Coven.Spellcasting.FileSystem.Posix` | Daemon (`PosixFSDaemon`), `UseFileSystem().UsePosix()` | Branch package |
| **Companion** | `Coven.Agents.FileSystem` | Tool definitions, transmuters | `Coven.Agents` + branch package |
| **Utility** | `Coven.Spellcasting` | `SpellDefinition`, `SchemaGen` | `Coven.Core` |

The companion bridges agents to the branch, not to a specific leaf. `Coven.Agents.FileSystem` provides transmuters from `AgentToolCall` → `FileRead` regardless of whether POSIX or Windows is backing the FileSystem branch.

---

## What Coven.Spellcasting Provides

A slim utility package:

| Type | Purpose |
|------|---------|
| `SpellDefinition` | Tool name + description + input/output JSON schemas |
| `SchemaGen` | Generate JSON schemas from CLR types |

No journal. No daemon. No branch manifest. No `ISpell`. No `Spellbook`.

Companion libraries use `SpellDefinition` to describe their tools. Agent leaves consume `SpellDefinition` to format LLM tool registrations.

---

## Companion Libraries

A companion library bridges agents to a tool branch. It provides:

1. **Tool definitions** — `SpellDefinition[]` describing available operations
2. **Transmuters** — route `AgentToolCall` → branch efferent entries, and branch afferent entries → `AgentToolResult`

```
Coven.Agents.FileSystem
  ├── FileSystemTools              → SpellDefinition[] for FileRead, FileWrite, etc.
  ├── AgentToolCallToFileRead      → transmuter
  ├── AgentToolCallToFileWrite     → transmuter
  ├── FileContentToAgentToolResult → transmuter
  └── FileFailureToAgentToolFailure → transmuter
```

Each transmuter returns null when the tool name doesn't match, so multiple routes from `AgentToolCall` coexist cleanly.

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

- [ ] Delete `ISpellContract`, `ISpell`, `Spellbook`, `SpellbookBuilder` (replaced by companion pattern)
- [ ] Slim `Coven.Spellcasting` to `SpellDefinition` + `SchemaGen`
- [ ] `Coven.Spellcasting.FileSystem` branch package with entry types
- [ ] `Coven.Spellcasting.FileSystem.Posix` leaf with daemon, `UseFileSystem().UsePosix()`
- [ ] `Coven.Spellcasting.Compute` branch package with entry types
- [ ] `Coven.Spellcasting.Compute.Posix` leaf with daemon, `UseCompute().UsePosix()`
- [ ] `Coven.Spellcasting.ImageGen` branch package with entry types
- [ ] `Coven.Spellcasting.ImageGen.Dalle` leaf with daemon, `UseImageGen().UseDalle()`
- [ ] `Coven.Agents.FileSystem` companion with tool definitions and transmuters
- [ ] `Coven.Agents.Compute` companion with tool definitions and transmuters
- [ ] `Coven.Agents.ImageGen` companion with tool definitions and transmuters
- [ ] Integration test: agent → companion → branch → leaf → branch → companion → agent round-trip
