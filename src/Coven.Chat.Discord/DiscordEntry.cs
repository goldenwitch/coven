// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.Chat.Discord;

/// <summary>
/// Base entry type for the Discord chat journal.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DiscordAck), nameof(DiscordAck))]
[JsonDerivedType(typeof(DiscordAfferent), nameof(DiscordAfferent))]
[JsonDerivedType(typeof(DiscordEfferent), nameof(DiscordEfferent))]
public abstract record DiscordEntry(
    string Sender
) : Entry;

/// <summary>Acknowledgement entry for internal synchronization.</summary>
public sealed record DiscordAck(
    string Sender,
    long Position
) : DiscordEntry(Sender);

/// <summary>Incoming Discord message received from a channel or DM.</summary>
public sealed record DiscordAfferent(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp) : DiscordEntry(Sender);

/// <summary>Outgoing Discord message to be sent.</summary>
public sealed record DiscordEfferent(
    string Sender,
    string Text) : DiscordEntry(Sender);
