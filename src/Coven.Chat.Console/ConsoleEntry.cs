// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.Chat.Console;

/// <summary>
/// Base entry type for the Console chat journal.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ConsoleAck), nameof(ConsoleAck))]
[JsonDerivedType(typeof(ConsoleAfferent), nameof(ConsoleAfferent))]
[JsonDerivedType(typeof(ConsoleEfferent), nameof(ConsoleEfferent))]
public abstract record ConsoleEntry(
    string Sender
) : Entry;

/// <summary>Acknowledgement entry for internal synchronization.</summary>
public sealed record ConsoleAck(
    string Sender,
    long Position
) : ConsoleEntry(Sender);

/// <summary>Incoming line read from stdin.</summary>
public sealed record ConsoleAfferent(
    string Sender,
    string Text
) : ConsoleEntry(Sender);

/// <summary>Outgoing line written to stdout.</summary>
public sealed record ConsoleEfferent(
    string Sender,
    string Text
) : ConsoleEntry(Sender);
