using Coven.Chat.Journal;

namespace Coven.Chat.Adapter.Discord;

// Minimal MVP ingress that appends HumanResponseEntry for button component interactions.
public sealed class DiscordIngress
{
    private readonly IAgentJournalStore _store;
    private readonly Func<DiscordComponentInteraction, Guid> _correlationResolver;

    public DiscordIngress(IAgentJournalStore store, Func<DiscordComponentInteraction, Guid> correlationResolver)
    {
        _store = store; _correlationResolver = correlationResolver;
    }

    public async Task<bool> TryHandleComponentAsync(DiscordComponentInteraction i, CancellationToken ct = default)
    {
        // custom_id: "coven|ask|{callId}|{option}"
        var parts = i.Data.CustomId.Split('|');
        if (parts.Length == 4 && string.Equals(parts[0], "coven", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[1], "ask", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(parts[2], "N", out var callId))
        {
            var corr = _correlationResolver(i);
            var fields = new Dictionary<string, string>
            {
                ["responderId"] = i.User.Id,
                ["responderName"] = i.User.Username
            };
            await _store.AppendAsync(corr, new HumanResponseEntry(callId, new HumanResponse(parts[3], fields), DateTimeOffset.UtcNow), ct);
            return true;
        }
        return false;
    }
}

