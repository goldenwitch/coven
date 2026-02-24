// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;
using Coven.Core;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Base entry type for LLamaSharp agent journals (requests, responses, chunks, acknowledgements).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LLamaSharpEfferent), nameof(LLamaSharpEfferent))]
[JsonDerivedType(typeof(LLamaSharpAfferent), nameof(LLamaSharpAfferent))]
[JsonDerivedType(typeof(LLamaSharpAfferentChunk), nameof(LLamaSharpAfferentChunk))]
[JsonDerivedType(typeof(LLamaSharpAck), nameof(LLamaSharpAck))]
[JsonDerivedType(typeof(LLamaSharpStreamCompleted), nameof(LLamaSharpStreamCompleted))]
public abstract record LLamaSharpEntry(string Sender) : Entry;

/// <summary>Outgoing request payload destined for the local LLamaSharp model.</summary>
public sealed record LLamaSharpEfferent(string Sender, string Text) : LLamaSharpEntry(Sender);

/// <summary>Incoming complete response from the local LLamaSharp model.</summary>
public sealed record LLamaSharpAfferent(
    string Sender,
    string Text,
    DateTimeOffset Timestamp,
    string Model) : LLamaSharpEntry(Sender);

/// <summary>Incoming streaming text chunk from the local LLamaSharp model.</summary>
public sealed record LLamaSharpAfferentChunk(
    string Sender,
    string Text,
    DateTimeOffset Timestamp,
    string Model) : LLamaSharpEntry(Sender), IDraft;

/// <summary>Acknowledgement used for synchronization.</summary>
public sealed record LLamaSharpAck(string Sender, long Position) : LLamaSharpEntry(Sender);

/// <summary>Marks completion of a streaming response from the local LLamaSharp model.</summary>
public sealed record LLamaSharpStreamCompleted(
    string Sender,
    DateTimeOffset Timestamp,
    string Model) : LLamaSharpEntry(Sender), IDraft;
