# Agent Tool Calls

> **Status**: Revised  
> **Created**: 2026-01-30  
> **Revised**: 2026-02-09

---

## Summary

Agents write tool call entries to their journal. The covenant routes them to the correct tool branch. Results route back. **Companion libraries** provide the types and transmuters that make this work.

---

## Agent Entry Types

The Agents branch adds three entry types for tool interactions:

```
ENTRY AgentEntry (extended)

  -- Existing
  AgentPrompt { sender, text }
  AgentResponse { sender, text }
  AgentThought { sender, text }

  -- Tool interaction
  AgentToolCall { correlation-id, tool-name, arguments }
  AgentToolResult { correlation-id, result }
  AgentToolFailure { correlation-id, error }
```

`AgentToolCall` carries the tool name and serialized arguments. The correlation ID ties a call to its result.

---

## Flow

When the LLM requests a tool call:

1. Agent leaf receives tool call from the LLM (tool name + JSON arguments)
2. Agent writes `AgentToolCall` to the agents journal
3. Covenant routes `AgentToolCall` → branch efferent entry (via companion transmuter)
4. Branch leaf daemon processes the efferent entry, writes afferent result to the branch journal
5. Covenant routes branch afferent entry → `AgentToolResult` (via companion transmuter)
6. Agent leaf receives `AgentToolResult`, feeds back to LLM
7. LLM continues

```
┌──────────────┐   AgentToolCall    ┌──────────────┐
│ Agents       │ ─────────────────▶ │ FileSystem   │
│ (journal)    │    [transmuter]    │ (journal)    │
│              │ ◀───────────────── │              │
└──────────────┘  AgentToolResult   └──────────────┘
                    [transmuter]
```

The transmuters live in the companion library. They convert between agent entry types and branch entry types.

---

## Synchronous Tool Semantics

From the LLM's perspective, tool calls are **synchronous within a turn**. The model expects results before generating the next response.

The agent leaf writes `AgentToolCall`, then waits. When `AgentToolResult` appears in the agents journal (routed back by the covenant), the leaf matches it by correlation ID and resumes.

Timeouts produce an `AgentToolFailure`, not an exception.

---

## Correlation Matching

The agent leaf tracks pending tool calls by correlation ID:

1. Write `AgentToolCall` with `correlation-id: new-guid()`
2. Tail the agents journal for `AgentToolResult` or `AgentToolFailure` matching that ID
3. On match, resume the LLM conversation with the result

Multiple concurrent tool calls (across different conversations) each have unique correlation IDs.

---

## Companion Library Role

The companion library (e.g., `Coven.Agents.FileSystem`) provides:

1. **Tool definitions** — `ToolDefinition[]` so the agent leaf can register tools with the LLM
2. **Forward transmuters** — convert `AgentToolCall` → branch efferent entry (e.g., `FileRead`)
3. **Return transmuters** — convert branch afferent entry (e.g., `FileContent`) → `AgentToolResult`

The forward transmuter inspects `AgentToolCall.ToolName` to determine if it handles this call. Non-matching transmuters return null (skip).

See [Spellcasting](spellcasting-branch.md) for companion library structure.

---

## Build-Time Example

```csharp
services.BuildCoven(coven =>
{
    var agents = coven.UseOpenAIAgents(agentConfig);
    var filesystem = coven.UseFileSystem(fs => fs.UsePosix(root: "/workspace"));

    coven.Covenant()
        .Connect(agents)
        .Connect(filesystem)
        .Routes(c =>
        {
            // Forward: agent tool calls → filesystem efferent entries
            c.Route<AgentToolCall, FileRead, AgentToolCallToFileRead>();
            c.Route<AgentToolCall, FileWrite, AgentToolCallToFileWrite>();

            // Return: filesystem afferent entries → agent tool results
            c.Route<FileContent, AgentToolResult, FileContentToAgentToolResult>();
            c.Route<FileFailure, AgentToolFailure, FileFailureToAgentToolFailure>();
        });
});
```

---

## Scope

**In scope:**
- `AgentToolCall`, `AgentToolResult`, `AgentToolFailure` entry types
- Correlation-based result matching in agent leaves
- Covenant routes between agents and tool branches

**Out of scope:**
- Parallel tool execution within a single LLM turn
- Tool call streaming (tool calls complete atomically)
- Agent-to-agent tool delegation

---

## Checklist

- [ ] `AgentToolCall` entry type with correlation ID, tool name, arguments
- [ ] `AgentToolResult` entry type with correlation ID, result
- [ ] `AgentToolFailure` entry type with correlation ID, error
- [ ] OpenAI leaf: write `AgentToolCall` on LLM tool_calls
- [ ] OpenAI leaf: correlation-based await for results
- [ ] Timeout → `AgentToolFailure`
- [ ] Integration test: full tool call round-trip
