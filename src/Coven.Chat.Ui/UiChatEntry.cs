// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.Chat.Ui;

/// <summary>
/// Base entry type for the UI chat journal.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(UiChatAck), nameof(UiChatAck))]
[JsonDerivedType(typeof(UiChatAfferent), nameof(UiChatAfferent))]
[JsonDerivedType(typeof(UiChatEfferent), nameof(UiChatEfferent))]
[JsonDerivedType(typeof(UiChatChunk), nameof(UiChatChunk))]
public abstract record UiChatEntry(
    string Sender
) : Entry;

/// <summary>Acknowledgement entry for internal synchronization.</summary>
public sealed record UiChatAck(
    string Sender,
    long Position
) : UiChatEntry(Sender);

/// <summary>Message submitted by the user through the UI.</summary>
public sealed record UiChatAfferent(
    string Sender,
    string Text
) : UiChatEntry(Sender);

/// <summary>Finalized message to render in the UI.</summary>
public sealed record UiChatEfferent(
    string Sender,
    string Text
) : UiChatEntry(Sender);

/// <summary>Streaming fragment to render incrementally in the UI.</summary>
public sealed record UiChatChunk(
    string Sender,
    string Text
) : UiChatEntry(Sender);
