# Coven.Spellcasting.Agents.Codex

High-level design and usage notes for `Coven.Spellcasting.Agents.Codex`.

## Data Flow

The agent acts as a bidirectional bridge. These two diagrams separate the user-side and Codex-side flows for clarity.

### A) User → ChatAdapter → CodexCliAgent
Note, there are two independent IScriveners here, one available to the CodexCliAgent and one available to the IAdapterHost.

Their T MUST match for them to share a journal but they can (and should) be separate instances of IScrivener, sometimes even different implementations.

#### A.1 User ↔ IAdapterHost
Note:
- A.1 is independent from everything described in our Codex agent.
- This functionality is defined via Coven.Chat
- We include this diagram here simply to connect what the agent's responsibilities are to a normal use pattern.
```
Outbound (1: User → Agent)
┌──────┐     ┌──────────────┐     ┌───────────────┐     ┌─────────┐
│ User │ ──► │ IAdapterHost │ ──► │ IScrivener<T> │ ──► │ Journal │
└──────┘     └──────────────┘     └───────────────┘     └─────────┘

Inbound (2: Journal → User)
┌─────────┐  Tailed by  ┌───────────────┐     ┌──────────────┐     ┌──────┐
│ Journal │ ──────────► │ IScrivener<T> │ ──► │ IAdapterHost │ ──► │ User │
└─────────┘             └───────────────┘     └──────────────┘     └──────┘
```

#### A.2 CodexCliAgent ↔ IAdapterHost
```
Outbound (3: Agent → User)
┌───────────────┐     ┌───────────────┐     ┌─────────┐
│ CodexCliAgent │ ──► │ IScrivener<T> │ ──► │ Journal │
└───────────────┘     └───────────────┘     └─────────┘

Inbound (4: Journal → Agent)
┌─────────┐  Tailed by  ┌───────────────┐     ┌───────────────┐
│ Journal │ ──────────► │ IScrivener<T> │ ──► │ CodexCliAgent │
└─────────┘             └───────────────┘     └───────────────┘
```

### B) CodexCliAgent → TailMux → Codex CLI

```
Outbound (Agent → Codex stdin):
┌───────────────┐   WriteLine(thought.Text)    ┌─────────┐   stdin   ┌───────────────────┐
│ CodexCliAgent │ ───────────────────────────► │ TailMux │ ────────► │ Codex CLI Process │
└───────────────┘                              └─────────┘           └───────────────────┘

Inbound (Codex rollout → Agent):
┌───────────────────┐  append JSONL  ┌────────────────────────────┐  tail lines ┌───────────────┐
│ Codex CLI Process │ ─────────────► │ .codex/codex.rollout.jsonl │ ──────────► │ CodexCliAgent │
└───────────────────┘                └────────────────────────────┘             └───────────────┘

TailMux provides: write-to-stdin and tail-from-file. Agent translates rollout lines → messages.
```

## Purpose

- Bridge Coven’s spellcasting runtime to a local Codex CLI process.
- Stream Codex “rollout” events back into Coven as messages.
- Optionally expose Coven Spells to Codex via a lightweight MCP server + shim.

## Responsibilities

- Use a deterministic rollout path at `<workspace>/.codex/codex.rollout.jsonl`.
- Tail the rollout JSONL and translate events to messages written via `IScrivener<TMessageFormat>`.
- Start Codex lazily via the TailMux when needed and write user thoughts to Codex stdin.
- If spells are registered, host an MCP server and merge Codex `config.toml` to point to the shim.
- Provide basic validation utilities to preflight the environment.

Summary mapping to the numbered flow above:
- (1) Listen: For `ChatEntry` journals only, `IScrivener<ChatEntry>.TailAsync` for `ChatThought` entries.
- (2) Write to user: `IScrivener<TMessageFormat>.WriteAsync` with translated rollout events.
- (3) Write to Codex: `ITailMux.WriteLineAsync` to Codex stdin.
- (4) Read from Codex: `ITailMux.TailAsync` from the deterministic rollout JSONL, parsed via `CodexRolloutParser`.

## Key Types (by role)

- Agent: `CodexCliAgent<TMessageFormat>` — core agent lifecycle and IO plumbing.
- DI: `CodexServiceCollectionExtensions` — registers the `ChatEntry` agent by default; typed overload enforces a translator.
- Translation: `ICodexRolloutTranslator<TMessageFormat>`, `DefaultChatEntryTranslator`, `DefaultStringTranslator`.
- Tail: `ITailMux`, `ProcessDocumentTailMux`, `DefaultTailMuxFactory` — tails a file and writes to stdin; owns process startup.
- Logging: `ILogger<CodexCliAgent<T>>` via DI; defaults to null logger.
- Rollout: `CodexRolloutParser`, `CodexRolloutLineKind`, `CodexRolloutLine`.
- MCP: `IMcpServerHost`, `LocalMcpServerHost`, `McpStdioServer`, `McpToolbelt`, `McpToolbeltBuilder`, `IMcpSpellExecutorRegistry`.
- Config: `ICodexConfigWriter`, `DefaultCodexConfigWriter` — merging/updating `config.toml`.
- Validation: `CodexCliValidation`, `CodexValidationPlanner`, `IValidationOps`.

## Lifecycle

1) Register spells (optional):
   - `CodexCliAgent.RegisterSpells(IReadOnlyList<ISpellContract>)` accepts actual spell instances (contracts), builds an MCP `McpToolbelt` from their definitions, and creates an executor registry so MCP tool calls can invoke the spells by name.

2) Invoke:
   - Compute `CODEX_HOME = <workspace>/.codex` and ensure directory.
   - If tools exist, start a local `IMcpServerHost` session (named pipe) and merge Codex `config.toml` to point the shim at that pipe.
   - Use a deterministic rollout path: `<workspace>/.codex/codex.rollout.jsonl`.
   - Create an `ITailMux` for that path; it owns Codex startup and writes stdin. Default mux launches `codex --log-dir <workspace>/.codex`.
   - Start two pipelines:
     - Egress: Tail rollout → parse to `CodexRolloutLine` → translate to `TMessageFormat` → `IScrivener<TMessageFormat>.WriteAsync`.
     - Ingress (ChatEntry only): `IScrivener<ChatEntry>.TailAsync` for `ChatThought` → send `.Text` to `ITailMux.WriteLineAsync` (Codex stdin).

3) Close:
   - Dispose the MCP session if created.
   - Dispose the multiplexer (terminates process if owned) and release resources.

## Message Format

- Typical format: `Coven.Chat.ChatEntry`; other formats supported via a translator.
  - Egress: Codex rollout → `CodexRolloutLine` → `ICodexRolloutTranslator<TMessageFormat>` → `IScrivener<TMessageFormat>`.
  - Ingress: Supported for `ChatEntry` only (`ChatThought` → Codex stdin) to avoid feedback loops for arbitrary types.

## MCP Integration

- Toolbelt: Built from `SpellDefinition` via `McpToolbeltBuilder.FromSpells`, preserving display names and JSON schemas from the Spellbook rather than reflection.
- Execution: If spell instances are supplied, `SimpleMcpSpellExecutorRegistry` deserializes args and invokes `ISpell<TIn, TOut>`, `ISpell<TIn>`, or `ISpell` at runtime; results are returned as `json` or `text` content to the MCP client.
- Transport: `LocalMcpServerHost` writes toolbelt JSON, opens a named pipe, performs a small PING/PONG handshake, then runs `McpStdioServer` over the pipe using a simple JSON-RPC loop with `Content-Length` framing.
- Codex Config: `ICodexConfigWriter.WriteOrMerge` updates `<CODEX_HOME>/config.toml` under `[mcp_servers.coven]` with `command = <shim>` and `args = ["<pipe>"]`.

## Rollout Tailing

- Path: Deterministic path `<workspace>/.codex/codex.rollout.jsonl`. Codex is launched with `--log-dir <workspace>/.codex` so the rollout file appears there.
- Tail Mux: `ProcessDocumentTailMux` is asymmetric:
  - Read: tails the rollout JSONL into a bounded channel; supports one consumer; reads from the start to include full session history, then continues tailing.
  - Write: lazily starts the Codex CLI process and writes lines to stdin.

## Process Launching

- Ownership: The tail mux owns process startup and lifecycle using the configured executable + arguments.
- Defaults: `DefaultTailMuxFactory` sets `CODEX_HOME=<workspace>/.codex` and launches `codex --log-dir <workspace>/.codex` in the configured workspace directory.

## Dependency Injection

- `AddCodexCliAgent(...)` registers the ChatEntry agent by default. It requires `IScrivener<ChatEntry>` and composes optional services:
  - `IMcpServerHost`, `ITailMuxFactory`, `ICodexConfigWriter`.
  - Optionally override `ICodexRolloutTranslator<ChatEntry>`; defaults to `DefaultChatEntryTranslator`.

- For non-ChatEntry message types, use the generic overload `AddCodexCliAgent<TMessage, TTranslator>()` and provide an `ICodexRolloutTranslator<TMessage>`.

## Validation

- `CodexCliValidation` uses `CodexValidationPlanner` for a deterministic plan, then executes via `IValidationOps`:
  - Probes: codex `--version`, workspace/codex-home writability, named-pipe handshake, optional shim `--help`, config merge, `sessions list`.
  - Returns an `AgentValidationResult` summarizing performed actions and notes.

## Error Handling

- Agent surfaces rollout parsing and tailing issues as `Error` lines, translated and written to the scrivener.
- Exceptions during ingress/egress loops are logged and written back via translator (ChatEntry).
- Disposal is best-effort; process and tail exceptions are non-fatal once reported.

## Minimal Usage

- DI:
  - `services.AddCodexCliAgent(o => { o.ExecutablePath = "codex"; o.WorkspaceDirectory = repo; /* o.ShimExecutablePath? o.Spells? */ });`
  - Ensure an `IScrivener<ChatEntry>` is registered; optionally register a custom `ICodexRolloutTranslator<ChatEntry>`.

## Notes

- Ensure the Codex CLI is on PATH or provide an absolute `ExecutablePath`.
- When exposing spells, include and/or publish the MCP shim under `mcp-shim/` alongside your app.
- On Windows, npm-installed `codex` frequently resolves to a `.cmd` shim; the agent accommodates this.
