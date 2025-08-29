using System.Collections.Concurrent;

namespace Coven.Chat.Adapter.Discord;

public interface IDiscordApi
{
    Task<DiscordMessage> SendAsync(string placeId, string text, DiscordComponents? components, CancellationToken ct);
    Task<DiscordMessage> EditAsync(string placeId, string messageId, string text, DiscordComponents? components, CancellationToken ct);
}

public sealed record DiscordMessage(string Id);

public abstract record DiscordComponents
{
    public static DiscordComponents Buttons(params (string Label, string CustomId)[] items) => new ButtonRow(items);
    public sealed record ButtonRow(IReadOnlyList<(string Label, string CustomId)> Items) : DiscordComponents;
}

public sealed record DiscordUser(string Id, string Username);

public sealed record DiscordComponentInteraction(DiscordUser User, DiscordComponentData Data);
public sealed record DiscordComponentData(string CustomId);

