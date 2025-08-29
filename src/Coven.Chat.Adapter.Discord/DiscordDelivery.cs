using System.Collections.Concurrent;
using Coven.Chat;

namespace Coven.Chat.Adapter.Discord;

public sealed class DiscordDelivery : IChatDelivery
{
    private readonly IDiscordApi _api;
    private readonly ConcurrentDictionary<string, bool> _seen = new();
    private readonly ConcurrentDictionary<(string ep, string place, string root, string key), string> _updates = new();

    public DiscordDelivery(IDiscordApi api) { _api = api; }

    public async ValueTask ApplyAsync(TranscriptRef where, OutboundChange change, string idempotencyKey, CancellationToken ct)
    {
        if (!_seen.TryAdd(idempotencyKey, true)) return;

        var key = (where.Endpoint, where.Place, where.RootMessageId, change.UpdateKey ?? string.Empty);
        switch (change.Mode)
        {
            case DeliveryMode.Append:
            {
                var msg = await _api.SendAsync(where.Place, change.Text, ComponentsFor(change), ct).ConfigureAwait(false);
                if (change.UpdateKey is not null) _updates[key] = msg.Id;
                break;
            }
            case DeliveryMode.Update:
            {
                if (change.UpdateKey is not null && _updates.TryGetValue(key, out var msgId))
                {
                    await _api.EditAsync(where.Place, msgId, change.Text, ComponentsFor(change), ct).ConfigureAwait(false);
                }
                else
                {
                    var msg = await _api.SendAsync(where.Place, change.Text, ComponentsFor(change), ct).ConfigureAwait(false);
                    if (change.UpdateKey is not null) _updates[key] = msg.Id;
                }
                break;
            }
        }
    }

    private static DiscordComponents? ComponentsFor(OutboundChange change)
    {
        if (!string.Equals(change.RenderKind, "ask", StringComparison.OrdinalIgnoreCase)) return null;
        if (change.Meta is null) return null;
        if (!change.Meta.TryGetValue("callId", out var callId)) return null;
        var options = change.Meta.TryGetValue("options", out var opts)
            ? SplitOptions(opts)
            : Array.Empty<string>();
        if (options.Count == 0) return null;
        return DiscordComponents.Buttons(options.Select(o => (o, $"coven|ask|{callId}|{o}")).ToArray());
    }

    private static IReadOnlyList<string> SplitOptions(string raw)
        => raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

