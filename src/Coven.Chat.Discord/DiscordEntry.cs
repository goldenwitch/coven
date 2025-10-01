namespace Coven.Chat.Discord;

// Base entry for Discord journal.
public abstract record DiscordEntry;

public sealed record DiscordIncoming(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp) : DiscordEntry;

public sealed record DiscordOutgoing(
    string Text) : DiscordEntry;
