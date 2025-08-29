using System.Collections.Concurrent;
using Coven.Chat.Journal;

namespace Coven.Chat;

public sealed record TranscriptRef(
    string Endpoint,
    string Place,
    string RootMessageId
);

public enum DeliveryMode { Append, Update }

public sealed record OutboundChange(
    DeliveryMode Mode,
    string Text,
    string? UpdateKey = null,
    string? RenderKind = null,
    IReadOnlyDictionary<string, string>? Meta = null
);

public interface IChatDelivery
{
    ValueTask ApplyAsync(TranscriptRef where, OutboundChange change, string idempotencyKey, CancellationToken ct);
}

public interface ITranscriptIndex
{
    bool TryGet(Guid correlationId, out TranscriptRef transcript);
}

public interface IInvocationBinder : ITranscriptIndex
{
    void Bind(Guid correlationId, TranscriptRef transcript);
}

public sealed class InMemoryInvocationBinder : IInvocationBinder
{
    private readonly ConcurrentDictionary<Guid, TranscriptRef> _map = new();
    public void Bind(Guid correlationId, TranscriptRef transcript) => _map[correlationId] = transcript;
    public bool TryGet(Guid correlationId, out TranscriptRef transcript) => _map.TryGetValue(correlationId, out transcript!);
}

public sealed class ChatJournalReader : IJournalReader
{
    private readonly ITranscriptIndex _index;
    private readonly IChatDelivery _delivery;
    public string ReaderId => "chat";

    public ChatJournalReader(ITranscriptIndex index, IChatDelivery delivery)
    { _index = index; _delivery = delivery; }

    public async ValueTask OnRecordAsync(JournalRecord record, CancellationToken ct)
    {
        if (!_index.TryGet(record.CorrelationId, out var where)) return;
        var change = Map(record.Entry);
        var idempotencyKey = $"{record.CorrelationId}:{record.Seq}";
        await _delivery.ApplyAsync(where, change, idempotencyKey, ct).ConfigureAwait(false);
    }

    internal static OutboundChange Map(AgentEntry e) => e switch
    {
        ThoughtEntry t   => new OutboundChange(DeliveryMode.Update, $"🧠 {t.Text}", UpdateKey: t.CoalesceKey, RenderKind: "thought"),
        ProgressEntry p  => new OutboundChange(DeliveryMode.Update, FormatProgress(p), UpdateKey: p.CoalesceKey, RenderKind: "progress"),
        ReplyEntry r     => new OutboundChange(DeliveryMode.Append, r.Text, RenderKind: "reply"),
        AskEntry a       => new OutboundChange(DeliveryMode.Append, FormatAsk(a), UpdateKey: a.CoalesceKey, RenderKind: "ask"),
        CompletedEntry c => new OutboundChange(DeliveryMode.Append, "✅ Completed.", RenderKind: "completed"),
        ErrorEntry err   => new OutboundChange(DeliveryMode.Append, $"🛑 {err.Message}", RenderKind: "error"),
        _                => new OutboundChange(DeliveryMode.Append, "ℹ️ (unhandled entry)")
    };

    private static string FormatProgress(ProgressEntry p)
    {
        var pct = p.Percent is null ? string.Empty : $" {(int)Math.Round((p.Percent.Value) * 100)}%";
        var stage = string.IsNullOrWhiteSpace(p.Stage) ? string.Empty : $" — {p.Stage}";
        var text = string.IsNullOrWhiteSpace(p.Text) ? string.Empty : $": {p.Text}";
        var s = $"⏳{pct}{stage}{text}".Trim();
        return string.IsNullOrWhiteSpace(s) ? "⏳" : s;
    }

    private static string FormatAsk(AskEntry a)
        => a.Ask.Options is { Count: > 0 } opts
            ? $"❓ {a.Ask.Prompt}  Options: {string.Join(", ", opts)}"
            : $"❓ {a.Ask.Prompt}";
}

