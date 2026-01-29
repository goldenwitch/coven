// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.Agents.Claude;

/// <summary>
/// Base entry type for Claude agent journals (requests, responses, thoughts, chunks, acknowledgements).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ClaudeEfferent), nameof(ClaudeEfferent))]
[JsonDerivedType(typeof(ClaudeAfferent), nameof(ClaudeAfferent))]
[JsonDerivedType(typeof(ClaudeAfferentChunk), nameof(ClaudeAfferentChunk))]
[JsonDerivedType(typeof(ClaudeAfferentThinkingChunk), nameof(ClaudeAfferentThinkingChunk))]
[JsonDerivedType(typeof(ClaudeThought), nameof(ClaudeThought))]
[JsonDerivedType(typeof(ClaudeAck), nameof(ClaudeAck))]
[JsonDerivedType(typeof(ClaudeStreamCompleted), nameof(ClaudeStreamCompleted))]
public abstract record ClaudeEntry(string Sender) : Entry;

/// <summary>Outgoing request payload destined for Claude.</summary>
public sealed record ClaudeEfferent(string Sender, string Text) : ClaudeEntry(Sender);

/// <summary>Incoming complete response from Claude.</summary>
public sealed record ClaudeAfferent(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp,
    string Model) : ClaudeEntry(Sender);

/// <summary>Incoming streaming text chunk from Claude.</summary>
public sealed record ClaudeAfferentChunk(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp,
    string Model) : ClaudeEntry(Sender), IDraft;

/// <summary>Incoming thinking/reasoning streaming chunk from Claude (extended thinking).</summary>
public sealed record ClaudeAfferentThinkingChunk(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp,
    string Model) : ClaudeEntry(Sender), IDraft;

/// <summary>Incoming thinking/thought summary from Claude (non-streamed).</summary>
public sealed record ClaudeThought(
    string Sender,
    string Text,
    string MessageId,
    DateTimeOffset Timestamp,
    string Model) : ClaudeEntry(Sender);

/// <summary>Acknowledgement used for synchronization.</summary>
public sealed record ClaudeAck(string Sender, long Position) : ClaudeEntry(Sender);

/// <summary>Marks completion of a streaming response from Claude.</summary>
public sealed record ClaudeStreamCompleted(
    string Sender,
    string MessageId,
    DateTimeOffset Timestamp,
    string Model) : ClaudeEntry(Sender), IDraft;
