# Coven.Chat.Journal.Compaction — Lightweight, Storage‑Agnostic Compaction

**Scope**: A minimal, safe compaction model for the append‑only journal used by Coven.Chat.Journal. No storage/DB specifics.

---

## 1) Purpose & Context

Coven’s chat integration is built on a **journal**: agents append immutable entries; readers project those entries into side effects (chat updates, logs, metrics). This document defines a compact, storage‑agnostic way to prune old entries **without breaking** projection, awaitables, or delivery semantics. It is designed to fit the contracts and flows defined in:

- *Coven.Chat* (chat projection, `OutboundChange`, coalescing/update semantics).  
- *Coven.Chat.Journal* (entries, `JournalRecord(Seq)`, `IJournalReader`, `ICheckpointStore`, barrier/waiter).

Compaction here means: **delete older, redundant entries** in a correlation’s stream while preserving the minimal prefix required to replay semantics correctly.

---

## 2) Terminology (brief)

- **Correlation**: a single invocation’s stream of entries.  
- **Seq**: monotonic sequence number assigned per correlation by the journal store.  
- **ReaderId**: stable id for a reader (e.g., `"chat"`, `"core"`).  
- **Checkpoint**: last applied `Seq` per `(ReaderId, CorrelationId)`.  
- **CoalesceKey**: stable key on entries like Thought/Progress to enable “update‑in‑place” behavior by readers/adapters.

---

## 3) Safety Invariants (must hold)

1. **Checkpoint gate**: Only compact entries with `seq ≤ safeUpTo`, where  
   `safeUpTo = min( checkpoint[readerId, correlationId] )` across all registered readers.  
   This ensures every reader has already applied those entries.

2. **Awaitables remain satisfiable**: Never drop a request that lacks a result. Keep any `AskEntry` or `OpRequestEntry` that does **not** yet have a corresponding `HumanResponseEntry` or `OpResultEntry`.

3. **Coalescibles keep the latest**: For entries with a `CoalesceKey` (e.g., Thought/Progress), you may drop older entries sharing the same key **as long as you retain the latest one** `≤ safeUpTo`.

4. **Terminal state preserved**: Always retain the most recent `CompletedEntry` or `ErrorEntry` (if present).

5. **Idempotent delivery remains correct**: Readers/adapters must already be idempotent per `idempotencyKey = "correlationId:seq"`. Compaction never deletes records **above** `safeUpTo`, so idempotent replay for newer entries is unchanged.

---

## 4) Retention Policy (default, simple)

- **Foldable entries (keep last per key)**  
  Keep only the latest `ThoughtEntry` and latest `ProgressEntry` for each distinct `CoalesceKey` (defaults `"thought"` and `"progress"`), drop older ones `≤ safeUpTo`.

- **Requests & results (pair‑aware)**  
  - **Keep** any `AskEntry` or `OpRequestEntry` with **no** matching result yet.  
  - For **answered** calls: keep the **result** (`HumanResponseEntry`/`OpResultEntry`). The paired request may be dropped immediately, or after a small grace period (TTL) for observability.

- **Replies (non‑coalescible, chatty)**  
  Keep the last **K** `ReplyEntry` (default `K=10`) `≤ safeUpTo`. Drop older replies to bound growth.

- **Terminal**  
  Keep the latest `CompletedEntry` **or** `ErrorEntry` if present.

- **Unknown entry kinds**  
  Treat as **non‑foldable** and retain unless you later introduce an explicit rule or a Snapshot (see §9).

This yields a minimal, semantically correct prefix that can still drive readers from a cold start.

---

## 5) Public API (tiny & optional)

```csharp
public sealed record CompactionPolicy(
    TimeSpan MinRecordAge,                 // only compact records older than this
    int KeepLastReplies = 10,
    TimeSpan? KeepAnsweredRequestTtl = null // optional grace before dropping answered requests
);

public sealed record CompactionReport(
    long Scanned, long Dropped, long Kept, long SafeUpTo);

public interface IJournalCompactor
{
    Task<CompactionReport> CompactAsync(Guid correlationId, CompactionPolicy policy, CancellationToken ct = default);
}
```

**Notes**  
- `MinRecordAge` avoids racing very fresh updates.  
- `KeepAnsweredRequestTtl` lets you retain answered requests briefly for audits while still pruning.

To remain storage‑agnostic, the compactor depends only on **read** + **checkpoint** + **delete** capabilities. Deletion is exposed via a tiny capability port (§6).

---

## 6) Storage Capability Port (no DB specifics)

Add a small, optional port that a storage implementation can provide to support physical deletion:

```csharp
public interface IJournalPruner
{
    // Delete all records for 'correlationId' with 'seq ≤ upToInclusive'
    // that are NOT present in 'keepSeqs' and are older than 'olderThanUtc'.
    // Returns the number of records deleted.
    ValueTask<long> PruneAsync(
        Guid correlationId,
        long upToInclusive,
        IReadOnlySet<long> keepSeqs,
        DateTimeOffset olderThanUtc,
        CancellationToken ct = default);
}
```

- In‑memory stores can implement this by rebuilding their per‑correlation list/queue.  
- Durable stores implement it using whatever batch deletion they support.  
- If `IJournalPruner` is **not** available, hosts can skip compaction or plug a custom implementation at the hosting layer.

---

## 7) Algorithm (per correlation id)

**Inputs**: `correlationId`, `CompactionPolicy policy`, `ICheckpointStore`, `IAgentJournalStore`, optional `IJournalPruner`, list of registered `ReaderId`s.

1. **Compute safe watermark**  
   `safeUpTo = min( checkpoint[rid, correlationId] )` across all readers. If no readers are registered, do nothing.

2. **Classify in one pass** over `ReadAsync(correlationId, fromExclusive: 0)` until `seq > safeUpTo`:
   - Track **latest‑by‑CoalesceKey** for foldables (Thought/Progress).
   - Track **all result seqs** (HumanResponse/OpResult).
   - Track **pending requests** (Ask/OpRequest) with no result yet.
   - Track **reply seqs**, remembering the **last K** per policy.
   - Track **latest terminal seq** (Completed/Error).

3. **Build keep‑set** (hash set of seq):
   - Latest foldable per key.  
   - All **result** entries.  
   - All **pending request** entries.  
   - Last **K** replies.  
   - Latest **terminal** (if any).  
   - Optionally, answered requests newer than `now - KeepAnsweredRequestTtl`.

4. **Prune**  
   If `IJournalPruner` is available, call `PruneAsync(correlationId, safeUpTo, keepSet, now - MinRecordAge)`.  
   Otherwise, no‑op (or log).

5. **Report**  
   Return counts and the `safeUpTo` watermark.

**Complexity**: O(n) over the compactable prefix; memory O(m) where m is size of keep‑set (typically small).

---

## 8) Scheduling

- **Rolling**: run every N minutes for active correlations with `MinRecordAge` ~ 2–5 minutes.  
- **End‑of‑run**: when a terminal entry is observed, run a final pass with tighter settings (e.g., `KeepLastReplies = 3`, `KeepAnsweredRequestTtl = 1h`).

Both modes are optional and host‑controlled. Compaction can also be triggered manually for a correlation id.

---

## 9) Optional Snapshot (future‑proofing, zero coupling)

If you later want a new reader to rebuild state **without** replaying the compacted prefix, you may introduce an internal `SnapshotEntry` that carries the latest folded values (last thought/progress, trimmed replies count, terminal).

- Readers that care about snapshots can use them to seed state.  
- `ChatJournalReader` SHOULD ignore snapshots (treat as a no‑op) to avoid chat noise.  
- You don’t need snapshots for this MVP; the keep‑set already makes replay cheap.

---

## 10) Interactions & Guarantees

- **Barriers remain correct**: Compaction only touches `seq ≤ safeUpTo`, the same watermark barriers rely on (reader checkpoints).  
- **Awaiters remain correct**: Pending requests are kept; results are kept; earliest matching result remains visible to waiters.  
- **Chat delivery remains correct**: Coalescing uses stable `UpdateKey`/`CoalesceKey`. If an adapter lacks native updates or loses its local update mapping, it can treat `Update` as an `Append` without violating idempotency.

---

## 11) Defaults (pragmatic)

- `MinRecordAge` = 2 minutes (rolling), 15 minutes (end‑of‑run).  
- `KeepLastReplies` = 10 (rolling), 3 (end‑of‑run).  
- `KeepAnsweredRequestTtl` = 1 hour (end‑of‑run).

These are safe starting points and easy to tune per host.

---

## 12) Test Matrix (essentials)

- **Safety**: ensure nothing beyond `safeUpTo` is removed.  
- **Awaitables**: pending `AskEntry` survives; after writing `HumanResponseEntry`, a subsequent compaction can drop the paired request (post‑TTL).  
- **Coalescing**: multiple Thought/Progress entries collapse to one per key; latest is kept.  
- **Replies**: keep exactly last K replies; older replies pruned.  
- **Terminal**: latest terminal is always retained.  
- **Idempotency**: replay after compaction still yields identical `OutboundChange` sequence for records `> safeUpTo`.

---

## 13) Optional Reference Skeleton (storage‑agnostic)

```csharp
public sealed class DefaultJournalCompactor : IJournalCompactor
{
    private readonly IAgentJournalStore _store;
    private readonly ICheckpointStore _ckpt;
    private readonly IJournalPruner? _pruner;
    private readonly Func<IReadOnlyList<string>> _readerIds;

    public DefaultJournalCompactor(
        IAgentJournalStore store,
        ICheckpointStore ckpt,
        IJournalPruner? pruner,
        Func<IReadOnlyList<string>> readerIdsProvider)
    {
        _store = store; _ckpt = ckpt; _pruner = pruner; _readerIds = readerIdsProvider;
    }

    public async Task<CompactionReport> CompactAsync(Guid corr, CompactionPolicy policy, CancellationToken ct = default)
    {
        var readers = _readerIds();
        if (readers.Count == 0) return new CompactionReport(0, 0, 0, 0);

        long safeUpTo = long.MaxValue;
        foreach (var rid in readers)
            safeUpTo = Math.Min(safeUpTo, await _ckpt.GetAsync(rid, corr, ct));
        if (safeUpTo <= 0) return new CompactionReport(0, 0, 0, safeUpTo);

        var now = DateTimeOffset.UtcNow;
        var foldableLatest = new Dictionary<string, long>(); // coalesceKey -> seq
        var resultSeqs = new HashSet<long>();
        var pending = new HashSet<Guid>();
        var answered = new HashSet<Guid>();
        var replies = new List<long>();
        long? terminalSeq = null;
        long scanned = 0;

        await foreach (var rec in _store.ReadAsync(corr, 0, ct))
        {
            if (rec.Seq > safeUpTo) break;
            scanned++;
            switch (rec.Entry)
            {
                case ThoughtEntry te when !string.IsNullOrEmpty(te.CoalesceKey):
                    foldableLatest[te.CoalesceKey!] = rec.Seq; break;
                case ProgressEntry pe when !string.IsNullOrEmpty(pe.CoalesceKey):
                    foldableLatest[pe.CoalesceKey!] = rec.Seq; break;
                case ReplyEntry:
                    replies.Add(rec.Seq); break;

                case AskEntry a: pending.Add(a.CallId); break;
                case OpRequestEntry o: pending.Add(o.CallId); break;

                case HumanResponseEntry hr:
                    answered.Add(hr.CallId); resultSeqs.Add(rec.Seq); break;
                case OpResultEntry orr:
                    answered.Add(orr.CallId); resultSeqs.Add(rec.Seq); break;

                case CompletedEntry or ErrorEntry:
                    terminalSeq = rec.Seq; break;
            }
        }

        var keep = new HashSet<long>(foldableLatest.Values);
        foreach (var s in replies.Skip(Math.Max(0, replies.Count - policy.KeepLastReplies)))
            keep.Add(s);
        foreach (var s in resultSeqs) keep.Add(s);

        // Second pass for requests to decide pending vs answered+TTL
        await foreach (var rec in _store.ReadAsync(corr, 0, ct))
        {
            if (rec.Seq > safeUpTo) break;
            switch (rec.Entry)
            {
                case AskEntry a when !answered.Contains(a.CallId):
                case OpRequestEntry o when !answered.Contains(o.CallId):
                    keep.Add(rec.Seq);
                    break;
                case AskEntry a when answered.Contains(a.CallId):
                case OpRequestEntry o when answered.Contains(o.CallId):
                    if (policy.KeepAnsweredRequestTtl is { } ttl && rec.Entry.AtUtc >= now - ttl)
                        keep.Add(rec.Seq);
                    break;
            }
        }

        if (terminalSeq is long t) keep.Add(t);

        long dropped = 0; long kept = keep.Count;
        if (_pruner is not null)
        {
            dropped = await _pruner.PruneAsync(corr, safeUpTo, keep, now - policy.MinRecordAge, ct);
        }

        return new CompactionReport(Scanned: scanned, Dropped: dropped, Kept: kept, SafeUpTo: safeUpTo);
    }
}
```

This skeleton compactor depends only on the **interfaces** already present in the journal design (plus the optional `IJournalPruner`) and makes no assumptions about storage technology.
