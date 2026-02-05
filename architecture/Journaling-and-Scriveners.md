# Journaling and Scriveners

Scriveners are append‑only journals that record typed entries. They decouple producers from consumers at architectural boundaries, enable replay/time‑travel, and make streaming deterministic.

## Why Journals?
- Audit and replay: every meaningful event is recorded for debugging and compliance.
- Decoupling: producers write entries without knowing who consumes them.
- Testing: deterministic inputs (append entries) yield deterministic outputs (tail reads).
- Streaming: incremental entries can be windowed/shattered into UX‑friendly outputs.

## Core Behaviors
- Append‑only: entries are added with a monotonically increasing position.
- Tail: readers observe `(position, entry)` tuples starting from a position (often `0` or the latest).
- Typed streams: each journal is strongly typed (e.g., `ChatEntry`, `AgentEntry`).

## Boundary Decoupling
- Spine ↔ Branch: MagikBlocks read/write `IScrivener<T>`; branches/leaves observe journals and act.
- Multi‑writer, multi‑reader: independent components coordinate only through entries, not callbacks.
- Recovery: restartable components can reconstruct state by replaying entries from position `0`.

## Patterns
- Directionality: efferent entries move away from the spine (outbound to leaves); afferent entries move toward the spine (inbound from leaves).
- Input/Output symmetry: design afferent vs efferent entries explicitly to clarify direction.
- Completion markers: write a dedicated “completed” entry to flush buffers and finalize windows.
- Idempotent consumers: consuming the same entry twice yields the same effect (or is a no‑op).
- Pure transmutation: map entries to entries with `ITransmuter`/`IBatchTransmuter` without side‑effects.

## Common Entry Families
- Chat: `ChatAfferent`, `ChatEfferentDraft`/`ChatEfferent`, `ChatChunk`, `ChatStreamCompleted`.
- Agents: `AgentPrompt`, `AgentThought`, `AgentResponse`, `AgentAfferentChunk`, `AgentAfferentThoughtChunk`.
- Daemons: `DaemonEvent` for status changes and failures.

## Operational Tips
- Use a single scrivener per flow in DI; avoid accidental duplicates.
- Avoid long‑running synchronous work in consumers; prefer daemons that tail asynchronously.
- Treat journals as the source of truth for cross‑component communication.

## Persistence
- In‑memory by default: `InMemoryScrivener<T>` (fast, replayable within process).
- File‑backed option: `Coven.Scriveners.FileScrivener` provides a background flusher that appends NDJSON snapshots to disk while your app continues to use the in‑memory journal.
  - See package README: `/src/Coven.Scriveners.FileScrivener/README.md`.
  - Try the toy: `/src/toys/Coven.Toys.FileScrivenerConsole/`.

## Related
- Windowing/Shattering: see “Windowing and Shattering”.
- Daemon lifecycle: see `Coven.Core.Daemonology` namespace.
