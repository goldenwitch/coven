// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents;

// Minimal agent entry union used with IScrivener<T>
public abstract record AgentEntry(string Sender);

// Unfixed/draft entries that should never be forwarded by adapters directly
public abstract record AgentEntryDraft(string Sender) : AgentEntry(Sender);

public sealed record AgentPrompt(string Sender, string Text) : AgentEntry(Sender);

public sealed record AgentResponse(string Sender, string Text) : AgentEntry(Sender);

public sealed record AgentThought(string Sender, string Text) : AgentEntry(Sender);

public sealed record AgentAck(string Sender) : AgentEntry(Sender);

// Streaming additions
public sealed record AgentEfferentChunk(string Sender, string Text) : AgentEntryDraft(Sender);

public sealed record AgentAfferentChunk(string Sender, string Text) : AgentEntryDraft(Sender);
public sealed record AgentEfferentThoughtChunk(string Sender, string Text) : AgentEntryDraft(Sender);

public sealed record AgentAfferentThoughtChunk(string Sender, string Text) : AgentEntryDraft(Sender);

public sealed record AgentStreamCompleted(string Sender) : AgentEntryDraft(Sender);
