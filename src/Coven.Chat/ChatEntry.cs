// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat;

// Minimal chat entry union used with IScrivener<T>
public abstract record ChatEntry(string Sender);

public sealed record ChatThought(string Sender, string Text) : ChatEntry(Sender);

public sealed record ChatResponse(string Sender, string Text) : ChatEntry(Sender);
