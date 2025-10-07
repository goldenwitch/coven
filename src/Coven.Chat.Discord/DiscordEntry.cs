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

public sealed record DiscordIncoming(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp) : DiscordEntry(Sender, Text);

public sealed record DiscordOutgoing(
    string Sender,
    string Text) : DiscordEntry(Sender, Text);
