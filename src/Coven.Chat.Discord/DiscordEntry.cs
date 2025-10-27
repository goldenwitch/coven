namespace Coven.Chat.Discord;

// Base entry for Discord journal.
public abstract record DiscordEntry(
    string Sender,
    string Text
);

public sealed record DiscordAck(
    string Sender,
    string Text
) : DiscordEntry(Sender, Text);

public sealed record DiscordAfferent(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp) : DiscordEntry(Sender, Text);

public sealed record DiscordEfferent(
    string Sender,
    string Text) : DiscordEntry(Sender, Text);
