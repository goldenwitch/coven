# Sample 02 — Console Claude FileSystem

Chat with a Claude agent in the console that can read files through the FileSystem branch via tool calls.

## What This Is (Coven terms)

- Chat Adapter: Uses `Coven.Chat.Console` to turn stdin/stdout into `ChatEntry` events.
- Agent Integration: Uses `Coven.Agents.Claude` with `EnableTools()` — Claude registers the companion's tool definitions and drives a tool-call loop (`tool_use` → journal → covenant → `tool_result`).
- FileSystem Branch: `Coven.FileSystem` defines `FileRead`/`FileContent`/`FileFailure` entries; `Coven.FileSystem.Posix` services them with a sandboxed daemon rooted at `FS_ROOT`.
- Companion Library: `Coven.Agents.FileSystem` bridges the two — `RouteFileSystemTools()` routes `AgentToolCall` → `FileRead` (with a validity predicate) and `FileContent`/`FileFailure` back to `AgentToolResult`/`AgentToolFailure`.
- Covenant: All routes are declared at DI time and validated at build time.

Key file:
- `Program.cs`: configuration, DI registration, and covenant wiring.

## Setup

Prerequisites:
- .NET 10 SDK installed.
- Anthropic API key.

Configure (env vars):
- `ANTHROPIC_API_KEY` (required)
- `CLAUDE_MODEL` (optional, defaults to `claude-sonnet-4-20250514`)
- `FS_ROOT` (optional sandbox root for file reads, defaults to the current directory)

## Run

```pwsh
dotnet run --project src/samples/02.ConsoleClaudeFileSystem
```

Ask the agent to read a file inside the sandbox, e.g. "read README.md and summarize it". Paths outside `FS_ROOT` (traversal, symlink escapes) are rejected by the daemon and surface to the agent as tool failures.
