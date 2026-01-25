# Architecture Guide

This folder documents only cross‑cutting architecture — the concepts and patterns that apply across the whole system. Package‑specific docs live next to their code.

Single takeaway: Coven provides Chat and Agent abstractions so your code talks to branches, not leaves. You integrate once with Chat/Agents and remain insulated from specific adapters like Discord or OpenAI.

## Critical Subtopics (Summaries)

- Daemons & Lifecycle: All long‑running work runs as daemons implementing a minimal lifecycle (Start/Shutdown with `Status` and failure journaling). Daemons are auto-started by `CovenExecutionScope` when a ritual begins, eliminating manual startup boilerplate. See: `src/Coven.Daemonology/README.md`.

- Journaling & Scriveners: Append‑only, typed journals decouple producers from consumers, enable replay/time‑travel, and make streaming deterministic. Blocks, branches, and leaves communicate by writing/reading entries, not by callbacks. See: [Journaling and Scriveners](./Journaling-and-Scriveners.md).

- Abstractions: Chat & Agents: Write app logic against branches (Chat/Agents) so you can swap leaves (Discord, Console, OpenAI) without touching your spine. Typed entries model afferent/efferent flows; templating and streaming are layered on top. See: [Abstractions and Branches](./Abstractions-and-Branches.md).

- Windowing & Shattering (Semantic Windowing): Policies define when buffered, streamed messages are ready for decision‑making. Optional shatter splits outputs (e.g., paragraphs). Completion markers ensure deterministic flush. See: [Windowing and Shattering](./Windowing-and-Shattering.md).

## Directionality (Afferent vs Efferent)

- Efferent: messages flowing away from your block code (spine) toward leaves (adapters/integrations).
- Afferent: messages flowing from leaves back toward your block code (spine).
- Rule of thumb: block/user code lives at the spine; efferent goes out, afferent comes in.

## Examples: Mapping Concepts to Sample 01 (Discord Agent)

Concrete examples help ground the vocabulary above. Two samples demonstrate different approaches:

**Sample 01 (Declarative)**: Defines routes declaratively at DI time via `BuildCoven` and covenants.
  - File: `src/samples/01.DiscordAgent/Program.cs`
  - No `RouterBlock` class—routes are defined inline and validated at build time.

**Sample 02 (Declarative, extended)**: Same pattern as Sample 01 with additional configuration examples.
  - File: `src/samples/02.DeclarativeDiscordAgent/Program.cs`

- Branches: app logic targets Chat and Agents abstractions.
  - Chat branch types used by the router: `ChatEntry`, `ChatAfferent`, `ChatEfferentDraft` (from `Coven.Chat`).
  - Agents branch types used by the router: `AgentEntry`, `AgentPrompt`, `AgentResponse`, `AgentThought` (from `Coven.Agents`).

- Leaves (adapters): concrete integrations that translate branches to external systems.
  - Discord chat: `Coven.Chat.Discord` (gateway/session/scrivener/daemon). Configured via `DiscordClientConfig` in `Program.cs`.
  - OpenAI agent: `Coven.Agents.OpenAI` (gateway/session/scrivener/daemon). Configured via `OpenAIClientConfig` in `Program.cs`.

- Daemons: long‑running services auto-started by `CovenExecutionScope` when the ritual begins.
  - Discord chat daemon (`DiscordChatDaemon`) tails Discord and writes `ChatAfferent` entries.
  - OpenAI agent daemon (`OpenAIAgentDaemon`) processes prompts and writes streamed agent chunks/entries.
  - Optional streaming daemons (from `Coven.Core.Streaming`) may be registered by branches to window chunk streams into outputs.

- Journals (Scriveners): append‑only logs connecting router↔branches↔leaves.
  - Chat journal: `IScrivener<ChatEntry>` (Discord adapter implements its own scrivener; router reads/writes entries).
  - Agent journal: `IScrivener<AgentEntry>` (OpenAI adapter scrivener; router reads/writes entries).
  - Persistence: use `Coven.Scriveners.FileScrivener` to append NDJSON snapshots to disk while keeping in‑process journals in memory.

- Directionality in the router logic:
  - Afferent (inbound): Discord adapter writes `ChatAfferent` when a user posts in the channel; router reads and forwards as an `AgentPrompt`.
  - Efferent (outbound): When the agent emits an `AgentResponse`, router writes a `ChatEfferentDraft` so the Discord adapter sends a message to the channel.
  - Thoughts: `AgentThought` entries are internal by default; the sample shows how to optionally surface them to chat.

- Streaming and windowing (semantic readiness):
  - OpenAI adapter streams agent output as chunks. Window policies (e.g., paragraph or max‑length) determine when to emit a user‑visible response.
  - Sample 01 demonstrates how to override windowing or templating via DI in `Program.cs`.

- Transmutation/templating examples:
  - Sample mapping of OpenAI entries to response items for prompt/answer templating: `DiscordOpenAITemplatingTransmuter` in `src/samples/01.DiscordAgent/DiscordOpenAITemplatingTransmuter.cs`.

- Configuration entry points (minimal app wire‑up):
  - File: `src/samples/01.DiscordAgent/Program.cs` — registers Discord chat and OpenAI agents, enables optional streaming behavior, and builds the ritual.

## Cancellation Tokens

- Optional parameter: Use `CancellationToken cancellationToken = default` as the last parameter on async APIs where cancellation is meaningful; avoid overload explosions.
- Propagation: Always forward the token to downstream calls; do not use `CancellationToken.None`.
- Background services: Honor the provided `stoppingToken` and pass it to all awaited operations.
- Linked tokens: Only link when composing multiple sources or applying an internal upper bound; dispose the CTS.
- Exceptions: Treat `OperationCanceledException` as cooperative shutdown; don’t log it as an error or wrap it.
- I/O: Prefer token-aware APIs; if missing, use a cancel-aware pattern (avoid `WaitAsync` if a better token overload exists).


## Documentation Standards

- Purpose: This folder documents cross‑cutting architecture only (concepts, lifecycles, policies, contracts, patterns). Avoid package‑specific API docs here.
- Location: Package/project documentation lives next to code at `src/<Package>/README.md` (usage, configuration, examples, changelogs).
- Structure: Keep a flat layout in `architecture/`;
- Titles: H1 names are topic‑based (e.g., `Windowing and Shattering`, `Journaling`), not package/namespace names.
- Scope: Show patterns with minimal examples; link to package READMEs for concrete types and APIs. Do not duplicate per‑package documentation.
- Independence: Cross‑cutting docs should remain implementation‑agnostic and stable across refactors; reference contracts and behaviors, not specific classes.
- Size: Keep each document focused and readable (target ~240 lines) to reduce drift and ease maintenance.
- Canonical patterns: Repo‑wide mandatory patterns live in the root [README](../README.md); use `architecture/` for deeper rationale and trade‑offs.
- Deprecations: Remove or relocate obsolete cross‑cutting docs immediately. Retire package‑specific pages from `architecture/` and move details to `src/<Package>/README.md`.
- Index hygiene: Keep this README and the repo [INDEX](../INDEX.md) up to date; link to both cross‑cutting topics and package READMEs when relevant.


## Topics
- Cross‑cutting topics live here. Current topics include the guidance in this file (e.g., Cancellation Tokens) and the pages below:
  - [Journaling and Scriveners](./Journaling-and-Scriveners.md)
  - [Abstractions and Branches](./Abstractions-and-Branches.md)
  - [Windowing and Shattering](./Windowing-and-Shattering.md)
  - [Covenants and Routing](./Covenants-and-Routing.md)

## Where to Find Package Docs
- Per‑package documentation: see `src/<Package>/README.md` (usage, configuration, examples).

## Meta/Misc
- [Licensing](./Licensing.md)
