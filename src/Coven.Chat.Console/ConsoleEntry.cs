// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;

namespace Coven.Chat.Console;

/// <summary>
/// Base entry type for the Console chat journal.
/// </summary>
public abstract record ConsoleEntry(
    string Sender,
    string Text
) : Entry;

/// <summary>Acknowledgement entry for internal synchronization.</summary>
public sealed record ConsoleAck(
    string Sender,
    string Text
) : ConsoleEntry(Sender, Text);

/// <summary>Incoming line read from stdin.</summary>
public sealed record ConsoleAfferent(
    string Sender,
    string Text
) : ConsoleEntry(Sender, Text);

/// <summary>Outgoing line written to stdout.</summary>
public sealed record ConsoleEfferent(
    string Sender,
    string Text
) : ConsoleEntry(Sender, Text);
