using Coven.Chat;
using Coven.Chat.Journal;

namespace Coven.Chat.Adapter.Discord;

public sealed class DiscordChatJournalReader : IJournalReader
{
    private readonly ITranscriptIndex _index;
    private readonly IChatDelivery _delivery;

    public string ReaderId => "chat";

    public DiscordChatJournalReader(ITranscriptIndex index, IChatDelivery delivery)
    { _index = index; _delivery = delivery; }

    public async ValueTask OnRecordAsync(JournalRecord record, CancellationToken ct)
    {
        if (!_index.TryGet(record.CorrelationId, out var where)) return;
        var change = Map(record.Entry);
        if (change is null) return;
        var idempotencyKey = $"{record.CorrelationId}:{record.Seq}";
        await _delivery.ApplyAsync(where, change, idempotencyKey, ct).ConfigureAwait(false);
    }

    public static OutboundChange? Map(AgentEntry e) => e switch
    {
        ThoughtEntry t   => new OutboundChange(DeliveryMode.Update, $"🧠 {t.Text}", UpdateKey: t.CoalesceKey, RenderKind: "thought"),
        ProgressEntry p  => new OutboundChange(DeliveryMode.Update, FormatProgress(p), UpdateKey: p.CoalesceKey, RenderKind: "progress"),
        ReplyEntry r     => new OutboundChange(DeliveryMode.Append, r.Text, RenderKind: "reply"),
        AskEntry a       => new OutboundChange(
                                DeliveryMode.Append,
                                FormatAsk(a),
                                UpdateKey: a.CoalesceKey,
                                RenderKind: "ask",
                                Meta: new Dictionary<string, string>
                                {
                                    ["callId"] = a.CallId.ToString("N"),
                                    ["options"] = a.Ask.Options is { Count: > 0 } opts ? string.Join(",", opts) : string.Empty
                                }),
        CompletedEntry c => new OutboundChange(DeliveryMode.Append, "✅ Completed.", RenderKind: "completed"),
        ErrorEntry err   => new OutboundChange(DeliveryMode.Append, $"🛑 {err.Message}", RenderKind: "error"),
        _ => null
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
