namespace Coven.Chat.Journal;

public sealed record CompactionPolicy(
    TimeSpan MinRecordAge,
    int KeepLastReplies = 10,
    TimeSpan? KeepAnsweredRequestTtl = null
);

public sealed record CompactionReport(long Scanned, long Dropped, long Kept, long SafeUpTo);

public interface IJournalPruner
{
    ValueTask<long> PruneAsync(
        Guid correlationId,
        long upToInclusive,
        IReadOnlySet<long> keepSeqs,
        DateTimeOffset olderThanUtc,
        CancellationToken ct = default);
}

public interface IJournalCompactor
{
    Task<CompactionReport> CompactAsync(Guid correlationId, CompactionPolicy policy, CancellationToken ct = default);
}

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
        {
            var ck = await _ckpt.GetAsync(rid, corr, ct).ConfigureAwait(false);
            safeUpTo = Math.Min(safeUpTo, ck);
        }
        if (safeUpTo <= 0) return new CompactionReport(0, 0, 0, safeUpTo);

        var now = DateTimeOffset.UtcNow;
        var foldableLatest = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var replies = new List<long>();
        var answered = new HashSet<Guid>();
        var resultSeqs = new HashSet<long>();
        long scanned = 0;
        long? terminalSeq = null;

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

                case AskEntry a:
                    // pending tracked later; only note answered when we see response
                    break;
                case OpRequestEntry o:
                    break;

                case HumanResponseEntry hr:
                    answered.Add(hr.CallId); resultSeqs.Add(rec.Seq); break;
                case OpResultEntry orr:
                    answered.Add(orr.CallId); resultSeqs.Add(rec.Seq); break;

                case CompletedEntry or ErrorEntry:
                    terminalSeq = rec.Seq; break;
            }
        }

        var keep = new HashSet<long>(foldableLatest.Values);
        foreach (var s in replies.Skip(Math.Max(0, replies.Count - policy.KeepLastReplies))) keep.Add(s);
        foreach (var s in resultSeqs) keep.Add(s);

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
            dropped = await _pruner.PruneAsync(corr, safeUpTo, keep, now - policy.MinRecordAge, ct).ConfigureAwait(false);
        }

        return new CompactionReport(Scanned: scanned, Dropped: dropped, Kept: kept, SafeUpTo: safeUpTo);
    }
}
