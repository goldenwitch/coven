// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat;

// Minimal chat entry union used with IScrivener<T>
public abstract record ChatEntry(string Sender, string Text);

public sealed record ChatOutgoing(string Sender, string Text) : ChatEntry(Sender, Text);

public sealed record ChatIncoming(string Sender, string Text) : ChatEntry(Sender, Text);

public sealed record ChatAck(string Sender, string Text) : ChatEntry(Sender, Text);
