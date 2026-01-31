# Agent-Spellcasting Integration

> **Status**: Draft  
> **Created**: 2026-01-30  
> **Depends on**: [Spellcasting Branch](spellcasting-branch.md), [Metagraph](metagraph.md)

---

## Summary

This proposal describes how the **Agents branch** integrates with the **Spellcasting branch** via covenant routes. Agent leaves write tool call entries; covenants route them to Spellcasting; results route back.

---

## Vocabulary

| Term | Definition |
|------|------------|
| **Branch** | Abstraction layer with typed journals + services (Chat, Agents, Spellcasting) |
| **Leaf** | Integration translating a branch to an external system (OpenAI, Gemini) |
| **Covenant** | Declarative routes between branch journals |

---

## New Agent Entry Types

The Agents branch needs new entry types for tool interactions:

```
ENTRY AgentEntry (extended)

  -- Existing types
  AgentPrompt { sender, text }
  AgentResponse { sender, text }
  AgentThought { sender, text }
  -- ... streaming chunks ...
  
  -- New: Tool interaction
  AgentToolCall { correlation-id, tool-name, arguments }
  AgentToolResult { correlation-id, result }
  AgentToolFault { correlation-id, error }
```

These entries flow through the Agents journal like any other `AgentEntry` subtype.

---

## Entry Flow

When the LLM requests a tool call, the agent leaf:

1. Matches tool name to capability (from build-time configuration)
2. Deserializes LLM arguments
3. Assigns a correlation ID
4. Writes an `AgentToolCall` entry to the Agents journal

```
LEAF OpenAIAgentLeaf
  ON LLM-response contains tool_calls:
    FOR each tool-call:
      cap = configured-capabilities[tool-call.name]
      WRITE AgentToolCall {
        correlation-id: new-guid(),
        tool-name: tool-call.name,
        arguments: tool-call.arguments
      } to AgentEntry journal
```

---

## Covenant Routes

The outer covenant connects Agents to Spellcasting:

```
COVENANT
  CONNECT agents
  CONNECT spellcasting
  
  -- Tool dispatch
  ROUTE AgentToolCall → SpellInvocation
  
  -- Result gathering  
  ROUTE SpellResult → AgentToolResult
  ROUTE SpellFault → AgentToolFault
```

The transformation from `AgentToolCall` to `SpellInvocation` deserializes the arguments into the appropriate spell type based on `tool-name`.

---

## Dispatch and Wait

The agent leaf writes a tool call and waits for its result before continuing the LLM conversation.

### Synchronous Tool Semantics

From the LLM's perspective, tool calls are **synchronous within a turn**:

1. LLM requests tool call
2. Leaf writes `AgentToolCall` entry
3. Covenant routes to Spellcasting, result routes back
4. Leaf receives `AgentToolResult`, feeds to LLM
5. LLM continues

This matches how LLM tool calling works—the model expects results before generating the next response.

### Correlation-Based Matching

The leaf maintains pending requests keyed by correlation ID. When `AgentToolResult` arrives (routed back by the covenant), the leaf matches it to the pending request and resumes the LLM conversation.

Timeouts produce a fault result, not an exception—the leaf handles it like any other tool failure.

### Journal Flow

```
┌─────────────┐    AgentToolCall    ┌─────────────────┐
│ Agents      │ ──────────────────▶ │ Spellcasting    │
│ (journal)   │                     │ (boundary)      │
│             │ ◀────────────────── │                 │
└─────────────┘  AgentToolResult    └─────────────────┘
```

1. Leaf writes `AgentToolCall` to Agents journal
2. Covenant routes to `SpellInvocation` on Spellcasting boundary
3. Inner covenant dispatches to substrate, result written
4. Inner covenant gathers result to `SpellResult` on boundary
5. Outer covenant routes to `AgentToolResult` on Agents journal
6. Leaf tails Agents journal, sees result, continues

---

## Usage Example

```
BUILD-COVEN
  chat = UseDiscordChat(config)
  agents = UseOpenAIAgents(agentConfig)
  spellcasting = UseSpellcasting()
  
  COVENANT
    CONNECT chat
    CONNECT agents
    CONNECT spellcasting
    
    -- Chat ↔ Agents
    ROUTE ChatAfferent → AgentPrompt
    ROUTE AgentResponse → ChatEfferentDraft
    
    -- Agents ↔ Spellcasting
    ROUTE AgentToolCall → SpellInvocation
    ROUTE SpellResult → AgentToolResult
    ROUTE SpellFault → AgentToolFault
```

The outer covenant connects branches. Each branch's internal structure is opaque.

---

## Scope

**In scope:**
- `AgentToolCall`, `AgentToolResult`, `AgentToolFault` entry types
- Covenant routes between Agents and Spellcasting
- Correlation-based result matching in agent leaves

**Out of scope:**
- Parallel tool execution
- Tool call streaming (tool calls complete atomically)
- Agent-to-agent tool delegation

---

## Checklist

- [ ] `AgentToolCall` entry type with correlation ID
- [ ] `AgentToolResult` entry type
- [ ] `AgentToolFault` entry type
- [ ] `AgentToolCall → SpellInvocation` transformation
- [ ] `SpellResult → AgentToolResult` transformation
- [ ] `SpellFault → AgentToolFault` transformation
- [ ] OpenAI leaf: handle `tool_calls` in streaming response
- [ ] OpenAI leaf: correlation-based await for results
- [ ] Timeout handling producing fault result
- [ ] Integration test: full tool call round-trip
