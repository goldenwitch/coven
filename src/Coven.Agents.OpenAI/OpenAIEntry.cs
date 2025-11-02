// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Base entry type for OpenAI agent journals (requests, responses, thoughts, chunks, acknowledgements).
/// </summary>
public abstract record OpenAIEntry(
    string Sender
);

/// <summary>Outgoing request payload destined for OpenAI.</summary>
public sealed record OpenAIEfferent(
    string Sender,
    string Text
) : OpenAIEntry(Sender);

/// <summary>Incoming response from OpenAI after completion.</summary>
public sealed record OpenAIAfferent(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);

/// <summary>Incoming response chunk (streaming) from OpenAI.</summary>
public sealed record OpenAIAfferentChunk(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);

// Streaming thought chunks (afferent): model streams thoughts back
/// <summary>Incoming thought chunk from OpenAI.</summary>
public sealed record OpenAIAfferentThoughtChunk(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);

/// <summary>Full thought message from OpenAI (non-chunked).</summary>
public sealed record OpenAIThought(
    string Sender,
    string Text,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);

/// <summary>OpenAI acknowledgement used for synchronization.</summary>
public sealed record OpenAIAck(
    string Sender,
    string Text
) : OpenAIEntry(Sender);

// Streaming thought chunks (efferent): agent streams thoughts out
/// <summary>Outgoing thought chunk destined for OpenAI (not forwarded by gateway today).</summary>
public sealed record OpenAIEfferentThoughtChunk(
    string Sender,
    string Text
) : OpenAIEntry(Sender);

/// <summary>Marks completion of a streaming response from OpenAI.</summary>
public sealed record OpenAIStreamCompleted(
    string Sender,
    string ResponseId,
    DateTimeOffset Timestamp,
    string Model
) : OpenAIEntry(Sender);
