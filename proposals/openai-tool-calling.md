# OpenAI Tool Calling

> **Status**: Draft  
> **Created**: 2026-02-09

---

## Summary

Add tool calling support to the OpenAI agent leaf. The OpenAI request gateway detects function call items in responses, writes provider-specific tool use entries, waits for results, and re-sends with function call output — matching the pattern established by the Claude agent.

---

## Motivation

The OpenAI agent uses the official `OpenAI` SDK (`OpenAI.Responses` namespace), which already includes function call types (`FunctionCallResponseItem`, `ResponseItem.CreateFunctionCallOutputItem`). The agent code simply does not use them yet. This is the lowest-effort provider to add tool support to.

---

## Design

### Entry Types

Two new entry types on `OpenAIEntry`:

```
OpenAIFunctionCall { sender, call-id, function-name, arguments-json, response-id, timestamp, model }
OpenAIFunctionResult { sender, call-id, result, is-error }
```

`call-id` maps to the SDK's function call item ID.

### Transmuter

`OpenAITransmuter` adds mappings:

| Direction | From | To |
|-----------|------|----|
| OpenAI → Agent | `OpenAIFunctionCall` | `AgentToolCall` |
| Agent → OpenAI | `AgentToolResult` | `OpenAIFunctionResult` |
| Agent → OpenAI | `AgentToolFailure` | `OpenAIFunctionResult` (error) |

### Registration

`OpenAIRegistration` gains `EnableTools()`, returning `this` for fluent chaining.

### Response Options

`OpenAIResponseOptionsTransmuter` adds tool definitions to `ResponseCreationOptions`. When `IEnumerable<ToolDefinition>` is injected and non-empty, each definition is converted to the SDK's function tool type and added to `options.Tools`.

### Gateway Tool Loop (Request)

`OpenAIRequestGatewayConnection` changes:

1. Inject `IEnumerable<ToolDefinition>` via constructor
2. Pass tools to `ResponseCreationOptions` via the options transmuter
3. After response, scan `response.OutputItems` for `FunctionCallResponseItem`
4. If found: write `OpenAIFunctionCall` entries, wait for `OpenAIFunctionResult` via `WaitForAsync`
5. Build function call output items from results
6. Re-send with original input + function call items + function call outputs
7. Loop until response contains no function calls

The OpenAI Responses API uses a flat item list (not nested message content blocks like Claude), so the re-send appends function call and output items to the input list.

### Manifest

When `ToolsEnabled`, `OpenAICovenBuilderExtensions` adds:

- `AgentToolCall` to produces set
- `AgentToolResult` and `AgentToolFailure` to consumes set

### Transcript Builder

`OpenAIEntryToResponseItemTransmuter` must handle `OpenAIFunctionCall` and `OpenAIFunctionResult` entries when rebuilding conversation history, mapping them to `FunctionCallResponseItem` and function call output items respectively.

### Virtual Gateway

`VirtualOpenAIGateway` gains:

- `EnqueueToolCallResponse(functionName, argumentsJson, followUpContent)` — scripts a response containing a function call, then the final text response after results arrive
- `ScriptedOpenAIToolCallResponse` scripting type implementing `IScriptedResponse`
- On `SendAsync`: emit `OpenAIFunctionCall` to journal, wait for `OpenAIFunctionResult`, then emit the final response

---

## Scope

**In scope:**
- `OpenAIFunctionCall` and `OpenAIFunctionResult` entry types
- `OpenAITransmuter` tool call/result mappings
- `OpenAIRegistration.EnableTools()`
- `OpenAIResponseOptionsTransmuter` tool definition injection
- `OpenAIRequestGatewayConnection` tool-call loop
- `OpenAICovenBuilderExtensions` manifest updates
- `OpenAIEntryToResponseItemTransmuter` tool entry handling
- `VirtualOpenAIGateway` tool call scripting
- E2E test: OpenAI tool call round-trip with FileSystem companion

**Out of scope:**
- Streaming gateway tool support (separate concern)
- Parallel tool execution within a turn
- New companion libraries
- Wire type changes (SDK already handles tool types)

---

## Dependencies

- [Agent Tool Calls](agent-spellcasting-integration.md) — defines `AgentToolCall`, `AgentToolResult`, `AgentToolFailure` (implemented)
- [Spellcasting Branch](spellcasting-branch.md) — defines companion library pattern (implemented for FileSystem)

---

## Checklist

- [ ] `OpenAIFunctionCall` entry type with `[JsonDerivedType]`
- [ ] `OpenAIFunctionResult` entry type with `[JsonDerivedType]`
- [ ] `OpenAITransmuter`: `OpenAIFunctionCall → AgentToolCall`
- [ ] `OpenAITransmuter`: `AgentToolResult → OpenAIFunctionResult`
- [ ] `OpenAITransmuter`: `AgentToolFailure → OpenAIFunctionResult` (error)
- [ ] `OpenAIRegistration.EnableTools()`
- [ ] `OpenAIResponseOptionsTransmuter`: inject tool definitions into `ResponseCreationOptions`
- [ ] `OpenAIRequestGatewayConnection`: inject tools, detect `FunctionCallResponseItem`, tool-call loop
- [ ] `OpenAICovenBuilderExtensions`: conditional `AgentToolCall`/`AgentToolResult` in manifest
- [ ] `OpenAIEntryToResponseItemTransmuter`: handle function call/result entries
- [ ] `VirtualOpenAIGateway`: `EnqueueToolCallResponse` scripting
- [ ] `ScriptedOpenAIToolCallResponse` type
- [ ] E2E test: OpenAI + FileSystem tool call round-trip
