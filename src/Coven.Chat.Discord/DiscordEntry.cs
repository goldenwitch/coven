namespace Coven.Chat.Discord;

// Base entry for Discord journal.
public abstract record DiscordEntry(
    string Text
);

public sealed record DiscordIncoming(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp) : DiscordEntry(Text);

public sealed record DiscordOutgoing(
    string Text) : DiscordEntry(Text);
