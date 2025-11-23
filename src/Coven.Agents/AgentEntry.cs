// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;

namespace Coven.Agents;

/// <summary>
/// Base entry type for agent journals (prompts, responses, thoughts, acks, and streaming chunks).
/// </summary>
public abstract record AgentEntry(string Sender) : Entry;

/// <summary>
/// Marker base for draft entries that should not be forwarded out of the agent journal directly.
/// </summary>
public abstract record AgentEntryDraft(string Sender) : AgentEntry(Sender), IDraft;

/// <summary>Represents a user or upstream prompt destined for an agent.</summary>
public sealed record AgentPrompt(string Sender, string Text) : AgentEntry(Sender);

/// <summary>Represents an agent's finalized response.</summary>
public sealed record AgentResponse(string Sender, string Text) : AgentEntry(Sender);

/// <summary>Represents an agent's introspective thought (not typically user-visible).</summary>
public sealed record AgentThought(string Sender, string Text) : AgentEntry(Sender);

/// <summary>Represents an acknowledgement for internal synchronization.</summary>
public sealed record AgentAck(string Sender) : AgentEntry(Sender);

// Streaming additions
/// <summary>Outgoing (efferent) response chunk prior to finalization.</summary>
public sealed record AgentEfferentChunk(string Sender, string Text) : AgentEntryDraft(Sender);

/// <summary>Incoming (afferent) response chunk prior to finalization.</summary>
public sealed record AgentAfferentChunk(string Sender, string Text) : AgentEntryDraft(Sender);
/// <summary>Outgoing (efferent) thought chunk prior to finalization.</summary>
public sealed record AgentEfferentThoughtChunk(string Sender, string Text) : AgentEntryDraft(Sender);

/// <summary>Incoming (afferent) thought chunk prior to finalization.</summary>
public sealed record AgentAfferentThoughtChunk(string Sender, string Text) : AgentEntryDraft(Sender);

/// <summary>Marks completion of a streaming sequence.</summary>
public sealed record AgentStreamCompleted(string Sender) : AgentEntryDraft(Sender);
