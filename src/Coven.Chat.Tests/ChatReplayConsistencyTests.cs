using System.Collections.Concurrent;
using Coven.Chat;
using Coven.Chat.Journal;
using Xunit;

namespace Coven.Chat.Tests;

file sealed class FakeDeliveryOrdered : IChatDelivery
{
    public List<OutboundChange> Changes { get; } = new();
    public ValueTask ApplyAsync(TranscriptRef where, OutboundChange change, string idempotencyKey, CancellationToken ct)
    {
        Changes.Add(change);
        return ValueTask.CompletedTask;
    }
}

file sealed class FixedIndex2 : ITranscriptIndex
{
    private readonly TranscriptRef _t;
    public FixedIndex2(TranscriptRef t) => _t = t;
    public bool TryGet(Guid correlationId, out TranscriptRef transcript) { transcript = _t; return true; }
}

public class ChatReplayConsistencyTests
{
    [Fact]
    public async Task ChangesAboveSafeUpToRemainIdenticalAfterCompaction()
    {
        var store = new InMemoryAgentJournalStore();
        var ckpt = new InMemoryCheckpointStore();
        var compactor = new DefaultJournalCompactor(store, ckpt, store, () => new[] { "chat" });
        var corr = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-30);

        // Seed a mixture of entries
        var seq = 0;
        async Task Append(AgentEntry e) { await store.AppendAsync(corr, e); seq++; }

        await Append(new ThoughtEntry("t1", t0, "thought"));             // 1
        await Append(new ThoughtEntry("t2", t0.AddMinutes(1), "thought")); // 2 (latest foldable ≤ safeUpTo)
        await Append(new ProgressEntry(0.1, null, null, t0.AddMinutes(2), "progress")); // 3
        await Append(new ProgressEntry(0.5, "stage", null, t0.AddMinutes(3), "progress")); // 4 (latest foldable ≤ safeUpTo)
        for (int i = 1; i <= 8; i++)                                        // 5..12 replies
            await Append(new ReplyEntry($"r{i}", t0.AddMinutes(4).AddSeconds(i)));

        // Set safeUpTo at 12 (after some replies)
        await ckpt.SetAsync("chat", corr, 12);

        // Add entries above safeUpTo (13..20)
        var callId = Guid.NewGuid();
        await Append(new AskEntry(callId, new HumanAsk("ok?"), t0.AddMinutes(6)));     // 13
        await Append(new HumanResponseEntry(callId, new HumanResponse("Yes"), t0.AddMinutes(7))); // 14
        for (int i = 9; i <= 12; i++)                                                   // 15..18 replies
            await Append(new ReplyEntry($"r{i}", t0.AddMinutes(8).AddSeconds(i)));
        await Append(new CompletedEntry(null, t0.AddMinutes(9)));                       // 19
        await Append(new ReplyEntry("tail", t0.AddMinutes(10)));                       // 20

        // Baseline mapping for records > safeUpTo
        var delivery1 = new FakeDeliveryOrdered();
        var reader1 = new ChatJournalReader(new FixedIndex2(new("x:endpoint", "place", "root")), delivery1);
        var snapshotBefore = store.Snapshot(corr).Where(r => r.Seq > 12).OrderBy(r => r.Seq).ToList();
        foreach (var rec in snapshotBefore)
            await reader1.OnRecordAsync(rec, default);

        // Compact (should not touch > safeUpTo)
        var report = await compactor.CompactAsync(corr, new CompactionPolicy(MinRecordAge: TimeSpan.FromMinutes(5), KeepLastReplies: 5), default);
        Assert.True(report.SafeUpTo >= 12);

        var delivery2 = new FakeDeliveryOrdered();
        var reader2 = new ChatJournalReader(new FixedIndex2(new("x:endpoint", "place", "root")), delivery2);
        var snapshotAfter = store.Snapshot(corr).Where(r => r.Seq > 12).OrderBy(r => r.Seq).ToList();
        foreach (var rec in snapshotAfter)
            await reader2.OnRecordAsync(rec, default);

        // Assert identical sequences of OutboundChange for > safeUpTo
        Assert.Equal(delivery1.Changes.Count, delivery2.Changes.Count);
        for (int i = 0; i < delivery1.Changes.Count; i++)
        {
            Assert.Equal(delivery1.Changes[i], delivery2.Changes[i]);
        }
    }
}

