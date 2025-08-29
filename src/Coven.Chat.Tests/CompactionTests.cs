using Coven.Chat.Journal;
using Xunit;

namespace Coven.Chat.Tests;

public class CompactionTests
{
    private static async Task<(Guid corr, InMemoryAgentJournalStore store, InMemoryCheckpointStore ckpt)> SeedAsync(params AgentEntry[] entries)
    {
        var store = new InMemoryAgentJournalStore();
        var ckpt = new InMemoryCheckpointStore();
        var corr = Guid.NewGuid();
        foreach (var e in entries)
            await store.AppendAsync(corr, e);
        return (corr, store, ckpt);
    }

    private static DefaultJournalCompactor Compactor(IAgentJournalStore store, ICheckpointStore ckpt)
        => new(store, ckpt, store as IJournalPruner, () => new[] { "chat" });

    [Fact]
    public async Task CoalescingKeepsLatestThoughtAndProgress()
    {
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var (corr, store, ckpt) = await SeedAsync(
            new ThoughtEntry("t1", t0, "thought"),
            new ThoughtEntry("t2", t0.AddMinutes(1), "thought"),
            new ProgressEntry(0.1, null, null, t0.AddMinutes(2), "progress"),
            new ProgressEntry(0.5, "stage", null, t0.AddMinutes(3), "progress")
        );

        // checkpoint at last entry so safeUpTo includes all
        await ckpt.SetAsync("chat", corr, 4);

        var report = await Compactor(store, ckpt).CompactAsync(corr, new CompactionPolicy(MinRecordAge: TimeSpan.FromMinutes(1)), default);
        Assert.Equal(4, report.SafeUpTo);
        Assert.True(report.Dropped >= 1); // earlier coalesced entries dropped

        // Ensure only latest per key remains
        var list = store.Snapshot(corr).ToList();
        var thoughts = list.Where(r => r.Entry is ThoughtEntry).ToList();
        var progress = list.Where(r => r.Entry is ProgressEntry).ToList();
        Assert.Single(thoughts);
        Assert.Single(progress);
        Assert.Equal("t2", ((ThoughtEntry)thoughts[0].Entry).Text);
        Assert.Equal(0.5, ((ProgressEntry)progress[0].Entry).Percent);
    }

    [Fact]
    public async Task RepliesKeepOnlyLastK()
    {
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var entries = Enumerable.Range(1, 15).Select(i => (AgentEntry)new ReplyEntry($"r{i}", t0.AddSeconds(i))).ToArray();
        var (corr, store, ckpt) = await SeedAsync(entries);
        await ckpt.SetAsync("chat", corr, 15);

        var report = await Compactor(store, ckpt).CompactAsync(corr, new CompactionPolicy(MinRecordAge: TimeSpan.FromMinutes(1), KeepLastReplies: 10), default);
        Assert.Equal(15, report.SafeUpTo);

        var list = store.Snapshot(corr).ToList();
        var replies = list.Where(r => r.Entry is ReplyEntry).Select(r => ((ReplyEntry)r.Entry).Text).ToList();
        Assert.Equal(10, replies.Count);
        Assert.Equal(Enumerable.Range(6, 10).Select(i => $"r{i}").ToArray(), replies.ToArray());
    }

    [Fact]
    public async Task PendingAskIsRetainedAnswerDropsRequest()
    {
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var call = Guid.NewGuid();
        var (corr, store, ckpt) = await SeedAsync(
            new AskEntry(call, new HumanAsk("ok?"), t0)
        );
        await ckpt.SetAsync("chat", corr, 1);
        var comp = Compactor(store, ckpt);

        // First compaction: no answer yet, keep ask
        await comp.CompactAsync(corr, new CompactionPolicy(TimeSpan.FromMinutes(1)), default);
        var list1 = store.Snapshot(corr).ToList();
        Assert.Single(list1.Where(r => r.Entry is AskEntry));

        // Append response and checkpoint
        await store.AppendAsync(corr, new HumanResponseEntry(call, new HumanResponse("Yes"), DateTimeOffset.UtcNow.AddMinutes(-5)));
        await ckpt.SetAsync("chat", corr, 2);

        // Compact with no TTL -> drop the answered request
        await comp.CompactAsync(corr, new CompactionPolicy(TimeSpan.FromMinutes(1), KeepAnsweredRequestTtl: null), default);
        var list2 = store.Snapshot(corr).ToList();
        Assert.Single(list2.Where(r => r.Entry is HumanResponseEntry));
        Assert.Empty(list2.Where(r => r.Entry is AskEntry));
    }

    [Fact]
    public async Task TerminalIsKept()
    {
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var (corr, store, ckpt) = await SeedAsync(
            new ReplyEntry("r1", t0),
            new CompletedEntry(null!, t0.AddMinutes(1))
        );
        await ckpt.SetAsync("chat", corr, 2);
        await Compactor(store, ckpt).CompactAsync(corr, new CompactionPolicy(TimeSpan.FromMinutes(1)), default);

        var list = store.Snapshot(corr).ToList();
        Assert.Contains(list, r => r.Entry is CompletedEntry);
    }
}
