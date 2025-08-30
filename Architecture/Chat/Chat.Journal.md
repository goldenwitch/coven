# Coven.Chat.Journal — Append‑Only Journal with Awaitables

## 1) Summary

Agents don’t talk to chat. They write **entries** to an append‑only journal keyed by `CorrelationId`.  
**Readers** project entries into side effects (chat updates, logs, metrics).  
Two tiny awaitable primitives make cross‑process operations easy:

1. **Projection Barrier** — wait until a named reader has *applied* an entry (e.g., “visible in chat”).  
2. **Result Wait** — wait until a *matching result entry* appears (e.g., human response or worker result).

This yields reliable, distributable **human‑in‑the‑loop** and **out‑of‑process** workflows without provider coupling.

---

## 2) Core Interfaces

### 2.1 IMagikJournal — ergonomic writer (returns awaitables)

```csharp
namespace Coven.Chat.Journal;

public interface IMagikJournal
{
    Guid CorrelationId { get; }

    ValueTask<AwaitHandle> Thought(string text, string? coalesceKey = "thought", CancellationToken ct = default);
    ValueTask<AwaitHandle> Progress(double? percent = null, string? stage = null, string? text = null,
                                    string? coalesceKey = "progress", CancellationToken ct = default);
    ValueTask<AwaitHandle> Reply(string text, CancellationToken ct = default);
    ValueTask<AwaitHandle> Completed(object? output = null, CancellationToken ct = default);
    ValueTask<AwaitHandle> Error(string message, string? stack = null, CancellationToken ct = default);

    // Awaitable “call → result” pairs
    ValueTask<AskAwaitable> Ask(HumanAsk ask, CancellationToken ct = default);
    ValueTask<OpAwaitable>  Operation(string operation, object payload, CancellationToken ct = default);
}
```

> `coalesceKey` is a hint for readers that want to update in place (e.g., keep one “thought” line current). Readers may ignore it.

### 2.2 Entries & Records

```csharp
public abstract record AgentEntry(DateTimeOffset AtUtc, string? CoalesceKey = null);

public sealed record ThoughtEntry(string Text, DateTimeOffset AtUtc, string? CoalesceKey = "thought")
    : AgentEntry(AtUtc, CoalesceKey);

public sealed record ProgressEntry(double? Percent, string? Stage, string? Text, DateTimeOffset AtUtc,
                                   string? CoalesceKey = "progress") : AgentEntry(AtUtc, CoalesceKey);

public sealed record ReplyEntry(string Text, DateTimeOffset AtUtc) : AgentEntry(AtUtc);
public sealed record CompletedEntry(object? Output, DateTimeOffset AtUtc) : AgentEntry(AtUtc);
public sealed record ErrorEntry(string Message, string? Stack, DateTimeOffset AtUtc) : AgentEntry(AtUtc);

// Awaitable request/response
public abstract record AwaitableEntry(Guid CallId, DateTimeOffset AtUtc, string? CoalesceKey = null) : AgentEntry(AtUtc, CoalesceKey);
public sealed record AskEntry(Guid CallId, HumanAsk Ask, DateTimeOffset AtUtc, string? CoalesceKey = "ask") : AwaitableEntry(CallId, AtUtc, CoalesceKey);
public sealed record HumanResponseEntry(Guid CallId, HumanResponse Response, DateTimeOffset AtUtc) : AgentEntry(AtUtc);
public sealed record OpRequestEntry(Guid CallId, string Operation, object Payload, DateTimeOffset AtUtc) : AwaitableEntry(CallId, AtUtc);
public sealed record OpResultEntry(Guid CallId, string Operation, object? Result, string? Error, DateTimeOffset AtUtc) : AgentEntry(AtUtc);

public sealed record JournalRecord(Guid CorrelationId, long Seq, AgentEntry Entry);
```

### 2.3 Storage & Readers

```csharp
public interface IAgentJournalStore
{
    ValueTask<long> AppendAsync(Guid correlationId, AgentEntry entry, CancellationToken ct = default);
    IAsyncEnumerable<JournalRecord> ReadAsync(Guid correlationId, long fromExclusive = 0, CancellationToken ct = default);
}

public interface IJournalReader
{
    string ReaderId { get; }
    ValueTask OnRecordAsync(JournalRecord record, CancellationToken ct);
}

public interface ICheckpointStore
{
    ValueTask<long> GetAsync(string readerId, Guid correlationId, CancellationToken ct = default);
    ValueTask SetAsync(string readerId, Guid correlationId, long seq, CancellationToken ct = default);
}
```

### 2.4 The Pump (host‑side)

```csharp
public sealed class JournalPump
{
    private readonly IAgentJournalStore _store;
    private readonly IEnumerable<IJournalReader> _readers;
    private readonly ICheckpointStore _ckpt;

    public JournalPump(IAgentJournalStore store, IEnumerable<IJournalReader> readers, ICheckpointStore ckpt)
    { _store = store; _readers = readers; _ckpt = ckpt; }

    public async Task DrainAsync(Guid correlationId, CancellationToken ct)
    {
        foreach (var reader in _readers)
        {
            var from = await _ckpt.GetAsync(reader.ReaderId, correlationId, ct);
            await foreach (var rec in _store.ReadAsync(correlationId, from, ct))
            {
                await reader.OnRecordAsync(rec, ct);
                await _ckpt.SetAsync(reader.ReaderId, correlationId, rec.Seq, ct);
            }
        }
    }
}
```

---

## 3) Awaitables

### 3.1 Projection Barrier

```csharp
public interface IJournalBarrier
{
    Task WhenAppliedAsync(Guid correlationId, long seq, string readerId,
                          TimeSpan? timeout = null, CancellationToken ct = default);
}
```

Completes when the named reader’s checkpoint for `(correlationId)` is **≥ `seq`**.

### 3.2 Result Waiter

```csharp
public interface IJournalWaiter
{
    Task<JournalRecord> WaitForAsync(Guid correlationId, long fromExclusive,
                                     Predicate<AgentEntry> match,
                                     TimeSpan? timeout = null, CancellationToken ct = default);
}
```

Awaits the first record **after** `fromExclusive` that satisfies `match`.

### 3.3 Handles returned by the journal

```csharp
public sealed record AppendReceipt(Guid CorrelationId, long Seq);

public sealed class AwaitHandle
{
    public AppendReceipt Receipt { get; }
    private readonly IJournalBarrier _barrier;
    private readonly IJournalWaiter _waiter;
    public AwaitHandle(AppendReceipt r, IJournalBarrier b, IJournalWaiter w) { Receipt = r; _barrier = b; _waiter = w; }

    public Task WhenAppliedAsync(string readerId, TimeSpan? timeout = null, CancellationToken ct = default)
        => _barrier.WhenAppliedAsync(Receipt.CorrelationId, Receipt.Seq, readerId, timeout, ct);

    public Task<JournalRecord> UntilAsync(Predicate<AgentEntry> match, TimeSpan? timeout = null, CancellationToken ct = default)
        => _waiter.WaitForAsync(Receipt.CorrelationId, Receipt.Seq, match, timeout, ct);
}

public sealed class AskAwaitable
{
    public AwaitHandle Handle { get; init; } = default!;
    public Guid CallId { get; init; }

    public async Task<HumanResponse> Response(TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var rec = await Handle.UntilAsync(e => e is HumanResponseEntry r && r.CallId == CallId, timeout, ct);
        return ((HumanResponseEntry)rec.Entry).Response;
    }
}

public sealed class OpAwaitable
{
    public AwaitHandle Handle { get; init; } = default!;
    public Guid CallId { get; init; }

    public async Task<OpResultEntry> Result(TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var rec = await Handle.UntilAsync(e => e is OpResultEntry r && r.CallId == CallId, timeout, ct);
        return (OpResultEntry)rec.Entry;
    }
}
```

### 3.4 Default journal implementation (sketch)

```csharp
public sealed class DefaultMagikJournal : IMagikJournal
{
    private readonly IAgentJournalStore _store;
    private readonly IJournalBarrier _barrier;
    private readonly IJournalWaiter _waiter;
    public Guid CorrelationId { get; }

    public DefaultMagikJournal(IAgentJournalStore store, IJournalBarrier barrier, IJournalWaiter waiter, Guid correlationId)
    { _store = store; _barrier = barrier; _waiter = waiter; CorrelationId = correlationId; }

    public async ValueTask<AwaitHandle> Thought(string text, string? coalesceKey, CancellationToken ct)
        => await Append(new ThoughtEntry(text, DateTimeOffset.UtcNow, coalesceKey), ct);

    public async ValueTask<AwaitHandle> Progress(double? percent, string? stage, string? text, string? coalesceKey, CancellationToken ct)
        => await Append(new ProgressEntry(percent, stage, text, DateTimeOffset.UtcNow, coalesceKey), ct);

    public async ValueTask<AwaitHandle> Reply(string text, CancellationToken ct)
        => await Append(new ReplyEntry(text, DateTimeOffset.UtcNow), ct);

    public async ValueTask<AwaitHandle> Completed(object? output, CancellationToken ct)
        => await Append(new CompletedEntry(output, DateTimeOffset.UtcNow), ct);

    public async ValueTask<AwaitHandle> Error(string message, string? stack, CancellationToken ct)
        => await Append(new ErrorEntry(message, stack, DateTimeOffset.UtcNow), ct);

    public async ValueTask<AskAwaitable> Ask(HumanAsk ask, CancellationToken ct)
    {
        var callId = Guid.NewGuid();
        var handle = await Append(new AskEntry(callId, ask, DateTimeOffset.UtcNow), ct);
        return new AskAwaitable { Handle = handle, CallId = callId };
    }

    public async ValueTask<OpAwaitable> Operation(string operation, object payload, CancellationToken ct)
    {
        var callId = Guid.NewGuid();
        var handle = await Append(new OpRequestEntry(callId, operation, payload, DateTimeOffset.UtcNow), ct);
        return new OpAwaitable { Handle = handle, CallId = callId };
    }

    private async ValueTask<AwaitHandle> Append(AgentEntry e, CancellationToken ct)
    {
        var seq = await _store.AppendAsync(CorrelationId, e, ct);
        return new AwaitHandle(new AppendReceipt(CorrelationId, seq), _barrier, _waiter);
    }
}
```

---

## 4) Human‑in‑the‑loop bridging

- **Ask path**: Agent appends `AskEntry(CallId, Prompt, …)` → Chat reader renders it with a continuation token → user acts → ingress appends `HumanResponseEntry(CallId, …)`.  
- **Await**: Agent awaits `AskAwaitable.Response()` which completes on the first matching response entry.

**Idempotency**: Use `CallId` as a unique key in ingress to avoid writing duplicate responses. If duplicates happen, the first wins; waiters take the earliest by sequence.

---

## 5) Reliability & Semantics

- **Ordering**: The journal store assigns monotonically increasing `Seq` per `CorrelationId`. Readers apply in order.  
- **At‑least‑once**: The pump advances `ICheckpointStore` after `OnRecordAsync` returns; on retry, idempotency keys prevent duplicate side effects.  
- **Barrier correctness**: `WhenAppliedAsync(corr, seq, readerId)` is satisfied *only after* the reader’s checkpoint reaches `seq`.  
- **Coalescing**: Entries with the same `CoalesceKey` may be rendered as updates by readers; the journal remains immutable.  
- **Timeouts**: Awaiters accept optional timeouts and cancellation tokens.

---

## 6) Storage Options

### In‑Memory (tests/dev)
- `ConcurrentQueue<JournalRecord>` per correlation; `long` counter for `Seq`.  
- Waiter implemented with `AsyncAutoResetEvent` per correlation.

### SQL (durable)
- **Journals** table: `(corr UUID, seq BIGINT, at_utc TIMESTAMPTZ, kind TEXT, entry JSONB, PRIMARY KEY (corr, seq))`.  
- **Checkpoints** table: `(reader_id TEXT, corr UUID, seq BIGINT, PRIMARY KEY (reader_id, corr))`.  
- **Results uniqueness**: optional `(call_id UUID, corr UUID, seq BIGINT)` table or a unique index inside `entry` JSON to guard against duplicate responses.

---

## 7) Example: Agent code

```csharp
public sealed class DesignAgent : MagikUser<NewDesignInput, DesignArtifact>
{
    private readonly IMagikJournal _journal;
    public DesignAgent(IMagikJournal journal) => _journal = journal;

    protected override async Task<DesignArtifact> InvokeAsync(NewDesignInput input, IBook<G> g, IBook<S> s, IBook<T> t, CancellationToken ct)
    {
        var t1 = await _journal.Thought($"Starting “{input.Title}”…");
        await t1.WhenAppliedAsync(readerId: "chat", timeout: TimeSpan.FromSeconds(5), ct);

        await _journal.Progress(0.25, "research", "Collecting references");
        await _journal.Reply("Draft 1 ready for review.");

        var ask = await _journal.Ask(new HumanAsk("Approve this palette?", new[] { "Approve", "Tweak" }), ct);
        var resp = await ask.Response(timeout: TimeSpan.FromMinutes(10), ct);

        // continue based on resp.Selected / resp.Fields...
        var artifact = await BuildAsync(input, ct);
        await _journal.Completed(artifact);
        return artifact;
    }
}
```

---

## 8) DI & Wiring

```csharp
services.AddSingleton<IAgentJournalStore, InMemoryAgentJournalStore>();  // or SQL
services.AddSingleton<ICheckpointStore, InMemoryCheckpointStore>();     // or SQL
services.AddSingleton<IJournalBarrier, DefaultJournalBarrier>();
services.AddSingleton<IJournalWaiter, DefaultJournalWaiter>();
services.AddScoped<IMagikJournal>(sp => new DefaultMagikJournal(
    sp.GetRequiredService<IAgentJournalStore>(),
    sp.GetRequiredService<IJournalBarrier>(),
    sp.GetRequiredService<IJournalWaiter>(),
    correlationId: ExecutionScope.CurrentCorrelationId));

// Readers
services.AddSingleton<IJournalReader, CoreJournalReader>();   // snapshot/logs
services.AddSingleton<IJournalReader, ChatJournalReader>();   // uses IChatDelivery from adapter
services.AddSingleton<JournalPump>();
```

---

## 9) CoreJournalReader (reference behavior)

Maintains a current transcript snapshot and logs for developers.

```csharp
public sealed record TranscriptView(
    string? LastThought,
    (double? Percent, string? Stage, string? Text)? Progress,
    IReadOnlyList<string> Replies,
    object? Output,
    string? Error,
    DateTimeOffset UpdatedAtUtc);

public interface ITranscriptSnapshot
{
    TranscriptView Get(Guid correlationId);
}

public sealed class CoreJournalReader : IJournalReader, ITranscriptSnapshot
{
    public string ReaderId => "core";
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TranscriptView> _state = new();

    public ValueTask OnRecordAsync(JournalRecord r, CancellationToken ct)
    {
        _state.AddOrUpdate(r.CorrelationId,
            addValueFactory: _ => Reduce(default, r.Entry),
            updateValueFactory: (_, current) => Reduce(current, r.Entry));
        return ValueTask.CompletedTask;
    }

    private static TranscriptView Reduce(TranscriptView current, AgentEntry e) => e switch
    {
        ThoughtEntry t   => current with { LastThought = t.Text, UpdatedAtUtc = DateTimeOffset.UtcNow },
        ProgressEntry p  => current with { Progress = (p.Percent, p.Stage, p.Text), UpdatedAtUtc = DateTimeOffset.UtcNow },
        ReplyEntry r     => current with { Replies = (current.Replies ?? Array.Empty<string>()).Append(r.Text).ToList(), UpdatedAtUtc = DateTimeOffset.UtcNow },
        CompletedEntry c => current with { Output = c.Output, UpdatedAtUtc = DateTimeOffset.UtcNow },
        ErrorEntry err   => current with { Error = err.Message, UpdatedAtUtc = DateTimeOffset.UtcNow },
        _                => current
    };

    public TranscriptView Get(Guid correlationId) => _state.TryGetValue(correlationId, out var v)
        ? v : new TranscriptView(null, null, Array.Empty<string>(), null, null, DateTimeOffset.UtcNow);
}
```
