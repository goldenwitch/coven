using System.Collections.Concurrent;
using Coven.Chat;
using Coven.Chat.Journal;
using Xunit;

namespace Coven.Chat.Tests;

file sealed class FakeDelivery : IChatDelivery
{
    public ConcurrentDictionary<string, (TranscriptRef Where, OutboundChange Change)> Applied { get; } = new();
    public ValueTask ApplyAsync(TranscriptRef where, OutboundChange change, string idempotencyKey, CancellationToken ct)
    {
        Applied.TryAdd(idempotencyKey, (where, change));
        return ValueTask.CompletedTask;
    }
}

file sealed class FixedIndex : ITranscriptIndex
{
    private readonly TranscriptRef _t;
    public FixedIndex(TranscriptRef t) => _t = t;
    public bool TryGet(Guid correlationId, out TranscriptRef transcript) { transcript = _t; return true; }
}

public class ChatJournalReaderTests
{
    [Fact]
    public async Task MapsThoughtToUpdate()
    {
        var delivery = new FakeDelivery();
        var reader = new ChatJournalReader(new FixedIndex(new("discord:alpha", "C1", "R1")), delivery);
        var rec = new JournalRecord(Guid.NewGuid(), 1, new ThoughtEntry("thinking...", DateTimeOffset.UtcNow, "thought"));
        await reader.OnRecordAsync(rec, default);
        Assert.True(delivery.Applied.TryGetValue($"{rec.CorrelationId}:{rec.Seq}", out var applied));
        Assert.Equal(DeliveryMode.Update, applied.Change.Mode);
        Assert.Equal("🧠 thinking...", applied.Change.Text);
        Assert.Equal("thought", applied.Change.UpdateKey);
        Assert.Equal("thought", applied.Change.RenderKind);
    }

    [Fact]
    public async Task MapsProgressFormatsPercentAndStage()
    {
        var delivery = new FakeDelivery();
        var reader = new ChatJournalReader(new FixedIndex(new("slack:beta", "C2", "R2")), delivery);
        var rec = new JournalRecord(Guid.NewGuid(), 2, new ProgressEntry(0.42, "stage", "doing", DateTimeOffset.UtcNow));
        await reader.OnRecordAsync(rec, default);
        var applied = delivery.Applied[$"{rec.CorrelationId}:{rec.Seq}"]; 
        Assert.Equal(DeliveryMode.Update, applied.Change.Mode);
        Assert.Contains("⏳ 42% — stage: doing", applied.Change.Text);
        Assert.Equal("progress", applied.Change.RenderKind);
    }

    [Fact]
    public async Task IdempotencyKeyPreventsDuplicateApply()
    {
        var delivery = new FakeDelivery();
        var reader = new ChatJournalReader(new FixedIndex(new("teams:gamma", "C3", "R3")), delivery);
        var corr = Guid.NewGuid();
        var rec = new JournalRecord(corr, 5, new ReplyEntry("hello", DateTimeOffset.UtcNow));
        await reader.OnRecordAsync(rec, default);
        // re-apply same record (simulate duplicate pump)
        await reader.OnRecordAsync(rec, default);
        // FakeDelivery dedupes on idempotency key
        Assert.Single(delivery.Applied);
        var key = $"{rec.CorrelationId}:{rec.Seq}";
        Assert.True(delivery.Applied.ContainsKey(key));
        Assert.Equal("reply", delivery.Applied[key].Change.RenderKind);
    }
}

