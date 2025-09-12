# Codex CLI Agent

High-level design and usage notes for `Coven.Spellcasting.Agents.Codex`.

## Data Flow

The agent acts as a bidirectional bridge. These two diagrams separate the user-side and Codex-side flows for clarity.

### A) User → ChatAdapter → CodexCliAgent

```
Outbound (1: User → Agent)
┌──────┐     ┌──────────────┐     ┌────────────────┐     ┌─────────┐
│ User │ ──► │ IAdapterHost │ ──► │  IScrivener    │ ──► | Journal │
└──────┘     └──────────────┘     └────────────────┘     └─────────┘

Inbound (2: Journal → User)
┌────────────┐            ┌─────────┐     ┌──────────────┐     ┌──────┐
│ IScrivener │ ──tails──► │ Journal │ ──► │ IAdapterHost │ ──► │ User │
└────────────┘            └─────────┘     └──────────────┘     └──────┘

Outbound (3: Agent → User)
┌───────────────┐     ┌────────────┐     ┌─────────┐
│ CodexCliAgent │ ──► │ IScrivener │ ──► │ Journal │
└───────────────┘     └────────────┘     └─────────┘

Inbound (4: Journal → Agent)
┌──────────────┐            ┌─────────┐     ┌───────────────┐
│  IScrivener  │ ──tails──► │ Journal │ ──► │ CodexCliAgent │
└──────────────┘            └─────────┘     └───────────────┘

```

### B) CodexCliAgent → TailMux → Codex CLI

```
Outbound (Agent → Codex stdin):
┌───────────────┐   WriteLine(thought.Text)    ┌─────────┐   stdin   ┌───────────────────┐
│ CodexCliAgent │ ───────────────────────────► │ TailMux │ ────────► │ Codex CLI Process │
└───────────────┘                              └─────────┘           └───────────────────┘

Inbound (Codex rollout → Agent):
┌───────────────────┐  append JSONL  ┌────────────────┐  tail lines   ┌───────────────┐
│ Codex CLI Process │ ─────────────► │ rollout-*.jsonl│ ───────────►  │ CodexCliAgent │
└───────────────────┘                └────────────────┘               └───────────────┘

TailMux provides: write-to-stdin and tail-from-file. Agent translates rollout lines → messages.
```

## Purpose

- Bridge Coven’s spellcasting runtime to a local Codex CLI process.
- Stream Codex “rollout” events back into Coven as messages.
- Optionally expose Coven Spells to Codex via a lightweight MCP server + shim.

## Responsibilities

- Start/stop the Codex CLI process with an isolated `CODEX_HOME` under the working repo.
- Tail Codex rollout JSONL and translate events to messages written via `IScrivener<T>`.
- For chat mode, forward user `ChatThought` messages to Codex stdin.
- If spells are registered, host an MCP server and write Codex `config.toml` that points to the shim.
- Provide basic validation utilities to preflight the environment.

Summary mapping to the numbered flow above:
- (1) Listen: `_scrivener.TailAsync` for `ChatThought` entries.
- (2) Write to user: `_scrivener.WriteAsync` with translated rollout events.
- (3) Write to Codex: `ITailMux.WriteLineAsync` to Codex stdin.
- (4) Read from Codex: `ITailMux.TailAsync` from rollout JSONL, parsed via `CodexRolloutParser`.

## Key Types (by role)

- Agent: `CodexCliAgent<TMessageFormat>` — core agent lifecycle and IO plumbing.
- DI/Builder: `CodexServiceCollectionExtensions`, `CodexCliAgentBuilder` — construction helpers.
- Translation: `ICodexRolloutTranslator<T>`, `DefaultStringTranslator`, `DefaultChatEntryTranslator`.
- Process: `ICodexProcessFactory`, `DefaultCodexProcessFactory` — starting Codex robustly on Windows/Linux.
- Tail: `ITailMux`, `ProcessDocumentTailMux`, `DefaultTailMuxFactory` — tails rollout file, writes to stdin.
- Rollout: `CodexRolloutParser`, `CodexRolloutLineKind`, `CodexRolloutLine`, `IRolloutPathResolver`.
- MCP: `IMcpServerHost`, `LocalMcpServerHost`, `McpStdioServer`, `McpToolbelt`, `McpToolbeltBuilder`, `IMcpSpellExecutorRegistry`.
- Config: `ICodexConfigWriter`, `DefaultCodexConfigWriter` — merging/updating `config.toml`.
- Validation: `CodexCliValidation`, `CodexValidationPlanner`, `IValidationOps`.

## Lifecycle

1) Register spells (optional):
   - `CodexCliAgent.RegisterSpells(List<SpellDefinition>)` stores definitions and builds an MCP `McpToolbelt` using the spellbook’s schemas and names.
   - If the agent is constructed with concrete spell instances, a `ReflectionMcpSpellExecutorRegistry` is also created to invoke spells by name.

2) Invoke:
   - Compute `CODEX_HOME = <workspace>/.codex` and ensure directory.
   - If tools exist, start a local `IMcpServerHost` session. This writes a `toolbelt-*.json` under `/.coven-mcp` and listens on a named pipe. Then write/merge Codex `config.toml` so the shim can connect to the pipe.
   - Start Codex via `ICodexProcessFactory.Start(...)` with the provided working directory and env.
   - Resolve rollout path via `IRolloutPathResolver`:
     - Probe `codex sessions list` (with platform-appropriate process launching) to extract `rollout-*.jsonl`.
     - Fallback: scan `CODEX_HOME/sessions` or use `<workspace>/codex.rollout.jsonl`.
   - Create an `ITailMux` to tail the rollout file and, if a process is available, write stdin lines to Codex.
   - Start two pipelines:
     - Egress: Tail rollout -> parse to `CodexRolloutLine` -> translate to `TMessageFormat` -> `IScrivener.WriteAsync`.
     - Ingress (chat only): Tail `IScrivener<T>.TailAsync` for `ChatThought` -> send `.Text` to `ITailMux.WriteLineAsync` (Codex stdin).

3) Close:
   - Dispose the MCP session if created.
   - Dispose the multiplexer (terminates process if owned) and release resources.

## Message Formats

- `TMessageFormat == string`:
  - Uses `DefaultStringTranslator` to render concise, single-line entries for rollout events.
  - Ingress is disabled to avoid echo loops (no chat semantics).

- `TMessageFormat == Coven.Chat.ChatEntry`:
  - Recommended for interactive adapters. Uses `DefaultChatEntryTranslator`.
  - Ingress enabled: only `ChatThought` entries are forwarded to Codex.

## MCP Integration

- Toolbelt: Built from `SpellDefinition` via `McpToolbeltBuilder.FromSpells`, preserving display names and JSON schemas from the Spellbook rather than reflection.
- Execution: If spell instances are supplied, `ReflectionMcpSpellExecutorRegistry` deserializes args and invokes `ISpell<TIn, TOut>`, `ISpell<TIn>`, or `ISpell` at runtime; results are returned as `json` or `text` content to the MCP client.
- Transport: `LocalMcpServerHost` writes toolbelt JSON, opens a named pipe, performs a small PING/PONG handshake, then runs `McpStdioServer` over the pipe using a simple JSON-RPC loop with `Content-Length` framing.
- Codex Config: `ICodexConfigWriter.WriteOrMerge` updates `<CODEX_HOME>/config.toml` under `[mcp_servers.coven]` with `command = <shim>` and `args = ["<pipe>"]`.

## Rollout Tailing

- Path Resolution: `DefaultRolloutPathResolver` attempts CLI session listing, then `~/.codex/sessions` scan, and finally a workspace fallback file. Windows and npm shims are handled explicitly.
- Tail Mux: `ProcessDocumentTailMux` is asymmetric:
  - Read: tails the rollout JSONL into a bounded channel; supports one consumer.
  - Write: lazily starts or reuses a process, writing lines to stdin; in agent usage the process is the Codex CLI.

## Process Launching and Discovery

- `DefaultCodexProcessFactory` starts the configured executable path. On Windows, it can prefer `cmd.exe /c` for `.cmd` shims, and it attempts discovery via:
  - `npm bin -g` to locate `codex` (prefers `codex.cmd` on Windows).
  - `where codex` on Windows to prefer `.cmd`, then `.exe`.
- `ExecutableDiscovery` contains shared helpers for PATH/npm probing.

## Dependency Injection

- `AddCodexCliAgent(...)` registers a string-based agent, auto-discovering an MCP shim path from `AppContext.BaseDirectory/mcp-shim` when needed, and composing optional services:
  - `IMcpServerHost`, `ICodexProcessFactory`, `ITailMuxFactory`, `ICodexConfigWriter`, `IRolloutPathResolver`.
- `AddCodexCliAgent<TMessage, TTranslator>(...)` registers a typed agent requiring an `ICodexRolloutTranslator<TMessage>` at compile time.

## Validation

- `CodexCliValidation` uses `CodexValidationPlanner` for a deterministic plan, then executes via `IValidationOps`:
  - Probes: codex `--version`, workspace/codex-home writability, named-pipe handshake, optional shim `--help`, config merge, `sessions list`.
  - Returns an `AgentValidationResult` summarizing performed actions and notes.

## Error Handling

- Agent surfaces rollout parsing and tailing issues as `Error` lines, translated and written to the scrivener.
- Exceptions during ingress/egress loops are logged and written back via translator (string/chat).
- Disposal is best-effort; process and tail exceptions are non-fatal once reported.

## Minimal Usage

- Direct construction for strings:
  - `CodexCliAgentBuilder.CreateString(exePath, workspaceDir, scrivener, shimPath?, spells?)`.

- DI:
  - `services.AddCodexCliAgent(o => { o.ExecutablePath = "codex"; o.WorkspaceDirectory = repo; /* o.ShimExecutablePath? o.Spells? */ });`
  - If using chat adapters, register an `IScrivener<ChatEntry>` and prefer the typed overload with a chat translator.

## Notes

- Ensure the Codex CLI is on PATH or provide an absolute `ExecutablePath`.
- When exposing spells, include and/or publish the MCP shim under `mcp-shim/` alongside your app.
- On Windows, npm-installed `codex` frequently resolves to a `.cmd` shim; the agent accommodates this.
