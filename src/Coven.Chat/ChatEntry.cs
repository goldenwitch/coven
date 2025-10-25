// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat;

// Minimal chat entry union used with IScrivener<T>
public abstract record ChatEntry(string Sender);

public sealed record ChatOutgoing(string Sender, string Text) : ChatEntry(Sender);

public sealed record ChatIncoming(string Sender, string Text) : ChatEntry(Sender);

public sealed record ChatAck(string Sender, string Text) : ChatEntry(Sender);

// Streaming additions
public sealed record ChatChunk(string Sender, string Text) : ChatEntry(Sender);

public sealed record ChatStreamCompleted(string Sender) : ChatEntry(Sender);
