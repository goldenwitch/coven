// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Console;

// Base entry for Console journal.
public abstract record ConsoleEntry(
    string Sender,
    string Text
);

public sealed record ConsoleAck(
    string Sender,
    string Text
) : ConsoleEntry(Sender, Text);

public sealed record ConsoleAfferent(
    string Sender,
    string Text
) : ConsoleEntry(Sender, Text);

public sealed record ConsoleEfferent(
    string Sender,
    string Text
) : ConsoleEntry(Sender, Text);

