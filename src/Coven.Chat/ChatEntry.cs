// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;

namespace Coven.Chat;

/// <summary>
/// Base entry type for chat journals (incoming/outgoing messages, acks, and streaming drafts/chunks).
/// </summary>
public abstract record ChatEntry(string Sender) : Entry;

/// <summary>
/// Marker base for draft entries that should not be forwarded by adapters directly.
/// </summary>
public abstract record ChatEntryDraft(string Sender) : ChatEntry(Sender), IDraft;

/// <summary>Outgoing chat message intended for users.</summary>
public sealed record ChatEfferent(string Sender, string Text) : ChatEntry(Sender);

/// <summary>Incoming chat message from users or external sources.</summary>
public sealed record ChatAfferent(string Sender, string Text) : ChatEntry(Sender);

/// <summary>Local acknowledgement to avoid feedback loops between journals.</summary>
public sealed record ChatAck(string Sender, string Text) : ChatEntry(Sender);

// Streaming additions
/// <summary>Outgoing draft message prior to finalization.</summary>
public sealed record ChatEfferentDraft(string Sender, string Text) : ChatEntryDraft(Sender);

/// <summary>Incoming draft message prior to finalization.</summary>
public sealed record ChatAfferentDraft(string Sender, string Text) : ChatEntryDraft(Sender);

/// <summary>Chunk of chat text for windowing and batching.</summary>
public sealed record ChatChunk(string Sender, string Text) : ChatEntryDraft(Sender);

/// <summary>Marks completion of a streaming sequence.</summary>
public sealed record ChatStreamCompleted(string Sender) : ChatEntryDraft(Sender);
