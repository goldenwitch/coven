// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.Agents.Gemini;

/// <summary>
/// Base entry type for Gemini agent journals (requests, responses, thoughts, chunks, acknowledgements).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GeminiEfferent), nameof(GeminiEfferent))]
[JsonDerivedType(typeof(GeminiAfferent), nameof(GeminiAfferent))]
[JsonDerivedType(typeof(GeminiAfferentChunk), nameof(GeminiAfferentChunk))]
[JsonDerivedType(typeof(GeminiAfferentReasoningChunk), nameof(GeminiAfferentReasoningChunk))]
[JsonDerivedType(typeof(GeminiThought), nameof(GeminiThought))]
[JsonDerivedType(typeof(GeminiAck), nameof(GeminiAck))]
[JsonDerivedType(typeof(GeminiStreamCompleted), nameof(GeminiStreamCompleted))]
[JsonDerivedType(typeof(GeminiSafetyBlock), nameof(GeminiSafetyBlock))]
public abstract record GeminiEntry(string Sender) : Entry;

/// <summary>Outgoing request payload destined for Gemini.</summary>
public sealed record GeminiEfferent(string Sender, string Text) : GeminiEntry(Sender);

/// <summary>Incoming complete response from Gemini.</summary>
public sealed record GeminiAfferent(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model) : GeminiEntry(Sender);

/// <summary>Incoming streaming text chunk from Gemini.</summary>
public sealed record GeminiAfferentChunk(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model) : GeminiEntry(Sender), IDraft;

/// <summary>Incoming reasoning/trace streaming chunk from Gemini.</summary>
public sealed record GeminiAfferentReasoningChunk(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model) : GeminiEntry(Sender), IDraft;

/// <summary>Incoming reasoning/thought summary from Gemini (non-streamed).</summary>
public sealed record GeminiThought(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model) : GeminiEntry(Sender);

/// <summary>Acknowledgement used for synchronization.</summary>
public sealed record GeminiAck(string Sender, long Position) : GeminiEntry(Sender);

/// <summary>Marks completion of a streaming response from Gemini.</summary>
public sealed record GeminiStreamCompleted(
    string Sender,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model) : GeminiEntry(Sender), IDraft;

/// <summary>Represents a safety block response from Gemini.</summary>
public sealed record GeminiSafetyBlock(
    string Sender,
    string Reason,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model) : GeminiEntry(Sender);
