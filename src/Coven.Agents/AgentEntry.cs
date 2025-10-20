// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents;

// Minimal agent entry union used with IScrivener<T>
public abstract record AgentEntry(string Sender, string Text);

public sealed record AgentPrompt(string Sender, string Text) : AgentEntry(Sender, Text);

public sealed record AgentResponse(string Sender, string Text) : AgentEntry(Sender, Text);

public sealed record AgentThought(string Sender, string Text) : AgentEntry(Sender, Text);

public sealed record AgentAck(string Sender, string Text) : AgentEntry(Sender, Text);

