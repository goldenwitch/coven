// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;

namespace Coven.Chat.Discord;

/// <summary>
/// Base entry type for the Discord chat journal.
/// </summary>
public abstract record DiscordEntry(
    string Sender,
    string Text
) : Entry;

/// <summary>Acknowledgement entry for internal synchronization.</summary>
public sealed record DiscordAck(
    string Sender,
    string Text
) : DiscordEntry(Sender, Text);

/// <summary>Incoming Discord message received from a channel or DM.</summary>
public sealed record DiscordAfferent(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp) : DiscordEntry(Sender, Text);

/// <summary>Outgoing Discord message to be sent.</summary>
public sealed record DiscordEfferent(
    string Sender,
    string Text) : DiscordEntry(Sender, Text);
