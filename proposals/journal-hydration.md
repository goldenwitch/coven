# Journal Hydration

> **Status**: Draft  
> **Created**: 2026-07-29

---

## Summary

Restore journal state from disk at startup, and give covenant pumps a configurable start position so hydrated history is not re-processed.

Two coupled changes:
1. `IJournalLoader<TEntry>` reads NDJSON written by [FileScrivener](../src/Coven.Scriveners.FileScrivener/README.md) back into the inner journal.
2. Covenant pumps start tailing from a high-water mark instead of position `0`.

---

## Motivation

[FileScrivener's README](../src/Coven.Scriveners.FileScrivener/README.md) states the gap plainly: persistence is append-only and *"reading/recovery from disk is not implemented — replay comes from the in-memory scrivener for the current process."*

Any application that outlives a single process — a desktop UI, a restarted service — loses its entire conversation on exit. This blocks [Coven UI Desktop](coven-ui-desktop.md), where switching agent providers requires tearing down and rebuilding the host (see [Agent Provider Switching](agent-provider-switching.md)).

### The re-processing hazard

Hydration alone is not enough, and getting this wrong is worse than having no persistence.

`RunPumpAsync` in [CovenantBuilder.cs](../src/Coven.Core/Covenants/CovenantBuilder.cs) tails from a hardcoded position `0`. If a journal is hydrated with history and pumps then start from `0`, every historical entry is re-routed: old prompts get re-sent to the model, old tool calls get re-executed against the file system.

`StreamWindowingDaemon` already does the right thing — its README documents that it tails *after the latest position*. Covenant pumps are the inconsistent case.

```
        hydrate 400 entries, pump starts at 0
        ─────────────────────────────────────
        pos 1    ChatAfferent "delete the logs"   ──▶ re-sent to model
        pos 2    AgentToolCall  delete_file       ──▶ re-executed
        ...
        pos 400  (history exhausted, live tailing begins)
```

---

## Design

### Loader

A loader hydrates the journal **before** any daemon starts. Ordering is the whole correctness story: `CovenExecutionScope.BeginScopeAsync` resolves and starts daemons on scope entry, so hydration must happen before that point.

```
INTERFACE IJournalLoader<TEntry>
  LoadAsync(target: IScrivener<TEntry>, ct) -> long   -- returns high-water position

PROCEDURE FileJournalLoader.LoadAsync(target, ct)
  IF source file does not exist:
    RETURN 0

  highWater = 0
  FOR EACH line IN read-lines(file):
    record = deserialize(line)

    IF record.schemaVersion NOT IN supported-versions:
      IF strict-mode: FAIL with schema mismatch
      ELSE: log-and-skip; CONTINUE

    IF record malformed:
      IF strict-mode: FAIL
      ELSE: log-and-skip; CONTINUE

    target.WriteAsync(record.entry)      -- positions reassigned sequentially
    highWater = highWater + 1

  RETURN highWater
```

Positions are **reassigned on write**, not restored. `InMemoryScrivener` owns position assignment (`++_nextPosition`) and exposes no seeding hook. Reassignment keeps positions dense and monotonic; the persisted `position` field becomes advisory only. Entries must therefore be replayed in file order, and correlation between processes must use domain identifiers (`CorrelationId`, `MessageId`), never positions.

### Pump start position

`CovenantAdherentDaemon` gains a start position per pump, defaulting to `0` so existing behavior is unchanged.

| Mode | Pump starts at | Use |
|------|----------------|-----|
| `FromBeginning` (default) | `0` | Current behavior; fresh in-memory journals |
| `FromLatest` | journal high-water at scope entry | Hydrated journals — history is context, not work |

`FromLatest` is resolved at scope entry, after hydration, before daemon start. The window between hydration and pump start contains no writes because no daemon is running yet.

### Sequencing

```
build host
  │
  ├─▶ hydrate journals            (IJournalLoader per registered type)
  │      └─ high-water marks recorded
  │
  ├─▶ BeginScopeAsync
  │      ├─ resolve daemons
  │      └─ start daemons ─▶ pumps tail from high-water
  │
  └─▶ ritual runs
```

### Growth

`InMemoryScrivener` retains every entry in a `List` for the process lifetime, with explicit `int.MaxValue` guards. Hydration makes this cumulative across restarts: a journal file that grows for weeks is fully resident in memory at startup.

`HistoryClip` does not help — it limits what a gateway *sends*, not what a journal *stores*.

Mitigations, in order of preference:

| Approach | Effect | Cost |
|----------|--------|------|
| Hydrate a bounded tail (last N entries) | Caps memory and startup time | Older history unreachable in-process |
| Per-session journal files | Natural bound; each session starts clean | Cross-session history needs explicit load |
| Compaction on load | Drops drafts/chunks/acks, keeps finalized entries | Loses streaming fidelity on replay |

Bounded-tail hydration plus per-session files is the recommended default. Compaction is attractive because chunk and draft entries (`IDraft` implementors) are transient by construction and carry no value once windowed — but it changes what replay means, so it stays opt-in.

---

## Scope

**In scope:**
- `IJournalLoader<TEntry>` abstraction and `FileJournalLoader<TEntry>` reading the NDJSON format `JsonEntrySerializer` writes
- Schema version validation with strict and lenient modes
- Bounded-tail hydration option
- Pump start position (`FromBeginning` / `FromLatest`) on `CovenantAdherentDaemon`
- Hydration hook ordered before daemon start
- Optional load-time compaction that drops `IDraft` entries

**Out of scope:**
- File rotation and on-disk compaction (FileScrivener remains append-only)
- Cross-process concurrent access — one process owns a file, unchanged
- On-disk format compatibility guarantees; `schemaVersion` gates readability
- Restoring original positions

---

## Dependencies

- [FileScrivener](../src/Coven.Scriveners.FileScrivener/README.md) NDJSON format and `IEntrySerializer<TEntry>` (implemented)
- `CovenantAdherentDaemon` pump construction (implemented)
- `CovenExecutionScope.BeginScopeAsync` daemon start ordering (implemented)

---

## Checklist

- [ ] `IJournalLoader<TEntry>` abstraction
- [ ] `FileJournalLoader<TEntry>` reading `{ schemaVersion, position, entry }` lines
- [ ] Schema version validation, strict and lenient modes
- [ ] Malformed-line tolerance with logging
- [ ] Bounded-tail hydration (last N entries)
- [ ] Optional `IDraft` compaction on load
- [ ] Pump start position on `CovenantAdherentDaemon`, default `FromBeginning`
- [ ] Hydration ordered before daemon start
- [ ] Test: hydrate, verify pumps do not re-route history
- [ ] Test: round-trip write → hydrate → entries match in order
- [ ] Test: schema mismatch rejected in strict mode, skipped in lenient
- [ ] `Coven.Scriveners.FileScrivener` README: remove the "not implemented" limitation note
