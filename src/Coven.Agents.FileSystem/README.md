# Coven.Agents.FileSystem

Companion library bridging Coven agents to the FileSystem branch — transmuters and covenant routing that let an LLM's tool calls flow through the file‑system journal.

## What's Inside

- `FileSystemTools`: static `ToolDefinition` constants (`ReadFile`) for registering file operations as agent tools.
- `AgentToolCallToFileRead`: transmuter converting an `AgentToolCall` into a `FileRead` entry (extracts `path` from JSON args).
- `FileContentToAgentToolResult`: transmuter converting a `FileContent` entry into an `AgentToolResult`.
- `FileFailureToAgentToolFailure`: transmuter converting a `FileFailure` entry into an `AgentToolFailure`.
- `RouteFileSystemTools`: extension on `ICovenant` that wires the three transmuter routes in one call.
- `AddFileSystemCompanion`: `IServiceCollection` extension that registers tool definitions and transmuters.

## Why use it?

- **Zero glue code**: call `AddFileSystemCompanion()` + `RouteFileSystemTools()` and agent tool calls automatically round‑trip through the FileSystem branch.
- **Type‑safe routing**: transmuters enforce correct entry mapping at compile time.
- **Provider‑independent**: works with any agent leaf (`Coven.Agents.OpenAI`, etc.) that emits `AgentToolCall` entries.

## Usage

```csharp
using Coven.Agents.FileSystem;

// Register transmuters and tool definitions
services.AddFileSystemCompanion();

// Inside covenant configuration
covenant.RouteFileSystemTools();
```

An agent leaf will see `read_file` as an available tool. When the model invokes it, the companion transmutes the call into a `FileRead`, the POSIX leaf services it, and the result flows back as an `AgentToolResult`.

## See Also

- Branch types: `Coven.FileSystem`.
- Leaf implementation: `Coven.FileSystem.Posix`.
- Agent abstractions: `Coven.Agents`.
- Architecture: Abstractions and Branches; Companions and Tool Registration.
