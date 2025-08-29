using System.Collections.Concurrent;

namespace Coven.Chat.Journal;

// Ergonomic writer returning awaitables
public interface IMagikJournal
{
    Guid CorrelationId { get; }

    ValueTask<AwaitHandle> Thought(string text, string? coalesceKey = "thought", CancellationToken ct = default);
    ValueTask<AwaitHandle> Progress(double? percent = null, string? stage = null, string? text = null,
                                    string? coalesceKey = "progress", CancellationToken ct = default);
    ValueTask<AwaitHandle> Reply(string text, CancellationToken ct = default);
    ValueTask<AwaitHandle> Completed(object? output = null, CancellationToken ct = default);
    ValueTask<AwaitHandle> Error(string message, string? stack = null, CancellationToken ct = default);

    ValueTask<AskAwaitable> Ask(HumanAsk ask, CancellationToken ct = default);
    ValueTask<OpAwaitable>  Operation(string operation, object payload, CancellationToken ct = default);
}

public abstract record AgentEntry(DateTimeOffset AtUtc, string? CoalesceKey = null);

public sealed record ThoughtEntry(string Text, DateTimeOffset AtUtc, string? CoalesceKey = "thought")
    : AgentEntry(AtUtc, CoalesceKey);

public sealed record ProgressEntry(double? Percent, string? Stage, string? Text, DateTimeOffset AtUtc,
                                   string? CoalesceKey = "progress") : AgentEntry(AtUtc, CoalesceKey);

public sealed record ReplyEntry(string Text, DateTimeOffset AtUtc) : AgentEntry(AtUtc);
public sealed record CompletedEntry(object? Output, DateTimeOffset AtUtc) : AgentEntry(AtUtc);
public sealed record ErrorEntry(string Message, string? Stack, DateTimeOffset AtUtc) : AgentEntry(AtUtc);

public abstract record AwaitableEntry(Guid CallId, DateTimeOffset AtUtc, string? CoalesceKey = null) : AgentEntry(AtUtc, CoalesceKey);
public sealed record AskEntry(Guid CallId, HumanAsk Ask, DateTimeOffset AtUtc, string? CoalesceKey = "ask") : AwaitableEntry(CallId, AtUtc, CoalesceKey);
public sealed record HumanResponseEntry(Guid CallId, HumanResponse Response, DateTimeOffset AtUtc) : AgentEntry(AtUtc);
public sealed record OpRequestEntry(Guid CallId, string Operation, object Payload, DateTimeOffset AtUtc) : AwaitableEntry(CallId, AtUtc);
public sealed record OpResultEntry(Guid CallId, string Operation, object? Result, string? Error, DateTimeOffset AtUtc) : AgentEntry(AtUtc);

public sealed record HumanAsk(string Prompt, IReadOnlyList<string>? Options = null, IReadOnlyDictionary<string,string>? Meta = null);
public sealed record HumanResponse(string? Selected = null, IReadOnlyDictionary<string,string>? Fields = null);

public sealed record JournalRecord(Guid CorrelationId, long Seq, AgentEntry Entry);

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

public sealed class JournalPump
{
    private readonly IAgentJournalStore _store;
    private readonly IEnumerable<IJournalReader> _readers;
    private readonly ICheckpointStore _ckpt;

    public JournalPump(IAgentJournalStore store, IEnumerable<IJournalReader> readers, ICheckpointStore ckpt)
    { _store = store; _readers = readers; _ckpt = ckpt; }

    public async Task DrainAsync(Guid correlationId, CancellationToken ct = default)
    {
        foreach (var reader in _readers)
        {
            var from = await _ckpt.GetAsync(reader.ReaderId, correlationId, ct).ConfigureAwait(false);
            await foreach (var rec in _store.ReadAsync(correlationId, from, ct))
            {
                await reader.OnRecordAsync(rec, ct).ConfigureAwait(false);
                await _ckpt.SetAsync(reader.ReaderId, correlationId, rec.Seq, ct).ConfigureAwait(false);
            }
        }
    }
}

public interface IJournalBarrier
{
    Task WhenAppliedAsync(Guid correlationId, long seq, string readerId,
                          TimeSpan? timeout = null, CancellationToken ct = default);
}

public interface IJournalWaiter
{
    Task<JournalRecord> WaitForAsync(Guid correlationId, long fromExclusive,
                                     Predicate<AgentEntry> match,
                                     TimeSpan? timeout = null, CancellationToken ct = default);
}

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
        var rec = await Handle.UntilAsync(e => e is HumanResponseEntry r && r.CallId == CallId, timeout, ct).ConfigureAwait(false);
        return ((HumanResponseEntry)rec.Entry).Response;
    }
}

public sealed class OpAwaitable
{
    public AwaitHandle Handle { get; init; } = default!;
    public Guid CallId { get; init; }

    public async Task<OpResultEntry> Result(TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var rec = await Handle.UntilAsync(e => e is OpResultEntry r && r.CallId == CallId, timeout, ct).ConfigureAwait(false);
        return (OpResultEntry)rec.Entry;
    }
}

// In-memory implementations for dev/tests
public sealed class InMemoryAgentJournalStore : IAgentJournalStore, IJournalPruner
{
    private sealed class Corr
    {
        public long Seq;
        public readonly List<JournalRecord> Records = new();
        public readonly object Gate = new();
        public readonly AsyncAutoResetEvent Signal = new(false);
    }

    private readonly ConcurrentDictionary<Guid, Corr> _byCorr = new();

    public ValueTask<long> AppendAsync(Guid correlationId, AgentEntry entry, CancellationToken ct = default)
    {
        var corr = _byCorr.GetOrAdd(correlationId, _ => new Corr());
        var next = Interlocked.Increment(ref corr.Seq);
        lock (corr.Gate)
        {
            corr.Records.Add(new JournalRecord(correlationId, next, entry));
        }
        corr.Signal.Set();
        return ValueTask.FromResult(next);
    }

    public async IAsyncEnumerable<JournalRecord> ReadAsync(Guid correlationId, long fromExclusive = 0, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var corr = _byCorr.GetOrAdd(correlationId, _ => new Corr());
        List<JournalRecord> snapshot;
        lock (corr.Gate)
        {
            snapshot = corr.Records
                .Where(r => r.Seq > fromExclusive)
                .OrderBy(r => r.Seq)
                .ToList();
        }
        foreach (var rec in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return rec;
        }
        await Task.CompletedTask;
    }

    // Testing/dev convenience: take a snapshot of current records for a correlation.
    public IReadOnlyList<JournalRecord> Snapshot(Guid correlationId)
    {
        if (!_byCorr.TryGetValue(correlationId, out var corr)) return Array.Empty<JournalRecord>();
        lock (corr.Gate)
        {
            return corr.Records.OrderBy(r => r.Seq).ToList();
        }
    }

    public ValueTask<long> PruneAsync(Guid correlationId, long upToInclusive, IReadOnlySet<long> keepSeqs, DateTimeOffset olderThanUtc, CancellationToken ct = default)
    {
        if (!_byCorr.TryGetValue(correlationId, out var corr)) return ValueTask.FromResult(0L);
        long before;
        lock (corr.Gate)
        {
            before = corr.Records.Count;
            corr.Records.RemoveAll(r => r.Seq <= upToInclusive && !keepSeqs.Contains(r.Seq) && r.Entry.AtUtc <= olderThanUtc);
        }
        var dropped = before - corr.Records.Count;
        return ValueTask.FromResult((long)dropped);
    }
}

public sealed class InMemoryCheckpointStore : ICheckpointStore
{
    private readonly ConcurrentDictionary<(string ReaderId, Guid Corr), long> _ckpt = new();
    public ValueTask<long> GetAsync(string readerId, Guid correlationId, CancellationToken ct = default)
        => ValueTask.FromResult(_ckpt.TryGetValue((readerId, correlationId), out var v) ? v : 0L);

    public ValueTask SetAsync(string readerId, Guid correlationId, long seq, CancellationToken ct = default)
    {
        _ckpt[(readerId, correlationId)] = seq;
        return ValueTask.CompletedTask;
    }
}

public sealed class DefaultJournalBarrier : IJournalBarrier
{
    private readonly ICheckpointStore _ckpt;
    public DefaultJournalBarrier(ICheckpointStore ckpt) => _ckpt = ckpt;

    public async Task WhenAppliedAsync(Guid correlationId, long seq, string readerId, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = timeout.HasValue ? DateTimeOffset.UtcNow + timeout.Value : (DateTimeOffset?)null;
        while (!ct.IsCancellationRequested)
        {
            var cur = await _ckpt.GetAsync(readerId, correlationId, ct).ConfigureAwait(false);
            if (cur >= seq) return;
            if (deadline is not null && DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException("Barrier wait timed out.");
            await Task.Delay(25, ct).ConfigureAwait(false);
        }
        ct.ThrowIfCancellationRequested();
    }
}

public sealed class DefaultJournalWaiter : IJournalWaiter
{
    private readonly IAgentJournalStore _store;
    public DefaultJournalWaiter(IAgentJournalStore store) => _store = store;

    public async Task<JournalRecord> WaitForAsync(Guid correlationId, long fromExclusive, Predicate<AgentEntry> match, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = timeout.HasValue ? DateTimeOffset.UtcNow + timeout.Value : (DateTimeOffset?)null;
        await foreach (var rec in _store.ReadAsync(correlationId, fromExclusive, ct))
        {
            if (match(rec.Entry)) return rec;
            if (deadline is not null && DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException("Result wait timed out.");
        }
        throw new OperationCanceledException("Wait canceled.");
    }
}

public sealed class DefaultMagikJournal : IMagikJournal
{
    private readonly IAgentJournalStore _store;
    private readonly IJournalBarrier _barrier;
    private readonly IJournalWaiter _waiter;
    public Guid CorrelationId { get; }

    public DefaultMagikJournal(IAgentJournalStore store, IJournalBarrier barrier, IJournalWaiter waiter, Guid correlationId)
    { _store = store; _barrier = barrier; _waiter = waiter; CorrelationId = correlationId; }

    public async ValueTask<AwaitHandle> Thought(string text, string? coalesceKey = "thought", CancellationToken ct = default)
        => await Append(new ThoughtEntry(text, DateTimeOffset.UtcNow, coalesceKey), ct).ConfigureAwait(false);

    public async ValueTask<AwaitHandle> Progress(double? percent = null, string? stage = null, string? text = null, string? coalesceKey = "progress", CancellationToken ct = default)
        => await Append(new ProgressEntry(percent, stage, text, DateTimeOffset.UtcNow, coalesceKey), ct).ConfigureAwait(false);

    public async ValueTask<AwaitHandle> Reply(string text, CancellationToken ct = default)
        => await Append(new ReplyEntry(text, DateTimeOffset.UtcNow), ct).ConfigureAwait(false);

    public async ValueTask<AwaitHandle> Completed(object? output = null, CancellationToken ct = default)
        => await Append(new CompletedEntry(output, DateTimeOffset.UtcNow), ct).ConfigureAwait(false);

    public async ValueTask<AwaitHandle> Error(string message, string? stack = null, CancellationToken ct = default)
        => await Append(new ErrorEntry(message, stack, DateTimeOffset.UtcNow), ct).ConfigureAwait(false);

    public async ValueTask<AskAwaitable> Ask(HumanAsk ask, CancellationToken ct = default)
    {
        var callId = Guid.NewGuid();
        var handle = await Append(new AskEntry(callId, ask, DateTimeOffset.UtcNow), ct).ConfigureAwait(false);
        return new AskAwaitable { Handle = handle, CallId = callId };
    }

    public async ValueTask<OpAwaitable> Operation(string operation, object payload, CancellationToken ct = default)
    {
        var callId = Guid.NewGuid();
        var handle = await Append(new OpRequestEntry(callId, operation, payload, DateTimeOffset.UtcNow), ct).ConfigureAwait(false);
        return new OpAwaitable { Handle = handle, CallId = callId };
    }

    private async ValueTask<AwaitHandle> Append(AgentEntry e, CancellationToken ct)
    {
        var seq = await _store.AppendAsync(CorrelationId, e, ct).ConfigureAwait(false);
        return new AwaitHandle(new AppendReceipt(CorrelationId, seq), _barrier, _waiter);
    }
}

// Simple async autoreset event for in-memory store
internal sealed class AsyncAutoResetEvent
{
    private readonly ConcurrentQueue<TaskCompletionSource> _waits = new();
    private volatile int _signaled;
    public AsyncAutoResetEvent(bool initial = false) { _signaled = initial ? 1 : 0; }
    public Task WaitAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _signaled, 0) == 1) return Task.CompletedTask;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _waits.Enqueue(tcs);
        ct.Register(() => tcs.TrySetCanceled(ct));
        // Try consume signal that may have arrived after enqueue
        if (Interlocked.Exchange(ref _signaled, 0) == 1)
        {
            if (_waits.TryDequeue(out var w)) w.TrySetResult();
        }
        return tcs.Task;
    }
    public void Set()
    {
        if (_waits.TryDequeue(out var w)) w.TrySetResult();
        else Interlocked.Exchange(ref _signaled, 1);
    }
}
