# Claude Streaming Tool Calling

> **Status**: Draft  
> **Created**: 2026-02-09

---

## Summary

Add tool calling support to Claude's streaming gateway. The streaming connection detects `tool_use` content blocks across SSE events, writes `ClaudeToolUse` entries, waits for `ClaudeToolResult`, and re-sends — extending the existing non-streaming tool loop to the SSE path.

---

## Motivation

Claude's request gateway (`ClaudeRequestGatewayConnection`) fully supports tool calling. The streaming gateway (`ClaudeStreamingGatewayConnection`) does not — it handles `text_delta` and `thinking_delta` events but ignores `tool_use` content blocks entirely. Applications using streaming with tools will silently drop tool calls.

The streaming gateway also does not inject `IEnumerable<ToolDefinition>` or set `Tools` on the request.

---

## Design

### SSE Tool Use Events

Claude's streaming API surfaces tool calls through the existing content block lifecycle:

1. `content_block_start` with `type: "tool_use"` — contains `id`, `name`
2. `content_block_delta` with `type: "input_json_delta"` — incremental JSON argument fragments
3. `content_block_stop` — signals the tool use block is complete

The streaming gateway must accumulate argument fragments per content block index, then emit the complete tool call on `content_block_stop`.

### Accumulation State

Per-request state tracks active content blocks:

```
STRUCTURE ContentBlockAccumulator
  blocks: map<index, { type, id?, name?, textBuffer, inputBuffer }>
  
  ON content_block_start(index, content_block):
    blocks[index] = { type: content_block.type, id: content_block.id, name: content_block.name }
    
  ON content_block_delta(index, delta):
    IF delta.type == "input_json_delta":
      blocks[index].inputBuffer.append(delta.partial_json)
    ELSE IF delta.type == "text_delta":
      blocks[index].textBuffer.append(delta.text)
    -- thinking_delta handled as before
    
  ON content_block_stop(index):
    block = blocks[index]
    IF block.type == "tool_use":
      EMIT ClaudeToolUse(block.id, block.name, block.inputBuffer)
    blocks.remove(index)
```

### Tool Loop

After the SSE stream completes (`message_stop`), the gateway checks if any `ClaudeToolUse` entries were written during this stream. If so:

1. Build the assistant message content (text blocks + tool_use blocks) from accumulated state
2. Wait for `ClaudeToolResult` for each pending tool use ID
3. Build the user message with `tool_result` content blocks
4. Append both messages to the conversation
5. Re-send as a new streaming request — loop until no tool_use blocks appear

This outer loop mirrors the request gateway. The inner SSE parsing stays single-pass.

### Constructor Change

`ClaudeStreamingGatewayConnection` adds `IEnumerable<ToolDefinition>` to its constructor and builds `ClaudeToolDefinition[]`, mirroring the request gateway.

### Request Change

The streaming request sets `Tools` when definitions are present, alongside the existing `Stream = true`.

### Virtual Gateway

`VirtualClaudeGateway` gains:

- `EnqueueStreamingToolCallResponse(toolName, argumentsJson, followUpChunks)` — scripts a streaming response containing a tool_use block, then streams the final response after tool results arrive
- `ScriptedClaudeStreamingToolCallResponse` scripting type
- On `SendAsync`: emit `ClaudeToolUse` to journal, wait for `ClaudeToolResult`, then emit streaming chunks for the follow-up

---

## Scope

**In scope:**
- `ClaudeStreamingGatewayConnection`: inject tools, set on request, accumulate tool_use blocks from SSE, tool-call loop
- Content block accumulator for `input_json_delta` events
- `VirtualClaudeGateway`: streaming tool call scripting
- `ScriptedClaudeStreamingToolCallResponse` type
- E2E test: Claude streaming + tool call round-trip

**Out of scope:**
- Changes to entry types (reuses existing `ClaudeToolUse` / `ClaudeToolResult`)
- Changes to transmuter (already handles tool entries)
- Changes to registration (already has `EnableTools()`)
- Changes to manifest (already conditional on `ToolsEnabled`)
- Non-streaming gateway (already implemented)

---

## Dependencies

- Claude request gateway tool calling (implemented)
- `ClaudeToolUse` / `ClaudeToolResult` entry types (implemented)
- `ClaudeTransmuter` tool mappings (implemented)

---

## Checklist

- [ ] `ClaudeStreamingGatewayConnection`: inject `IEnumerable<ToolDefinition>`
- [ ] `ClaudeStreamingGatewayConnection`: set `Tools` on streaming request
- [ ] Content block accumulator: track `tool_use` blocks across SSE events
- [ ] Handle `input_json_delta` delta type in `HandleContentDelta`
- [ ] Emit `ClaudeToolUse` on `content_block_stop` for tool_use blocks
- [ ] Outer tool-call loop: detect pending tool calls after stream ends, wait, re-send
- [ ] `VirtualClaudeGateway`: `EnqueueStreamingToolCallResponse`
- [ ] `ScriptedClaudeStreamingToolCallResponse` scripting type
- [ ] E2E test: streaming + tool call round-trip
