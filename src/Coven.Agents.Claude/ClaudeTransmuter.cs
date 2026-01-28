// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.Claude;

/// <summary>
/// Maps between Claude-specific entries and generic Agent entries using position-imbued ACKs.
/// Afferent: Claude → Agent; Efferent: Agent → Claude.
/// </summary>
internal sealed class ClaudeTransmuter
    : IImbuingTransmuter<ClaudeEntry, long, AgentEntry>,
      IImbuingTransmuter<AgentEntry, long, ClaudeEntry>
{
    public Task<AgentEntry> Transmute(ClaudeEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ClaudeAfferent incoming => Task.FromResult<AgentEntry>(new AgentResponse(incoming.Sender, incoming.Text)),
            ClaudeAfferentChunk chunk => Task.FromResult<AgentEntry>(new AgentAfferentChunk(chunk.Sender, chunk.Text)),
            ClaudeAfferentThinkingChunk thinking => Task.FromResult<AgentEntry>(new AgentAfferentThoughtChunk(thinking.Sender, thinking.Text)),
            ClaudeThought thought => Task.FromResult<AgentEntry>(new AgentThought(thought.Sender, thought.Text)),
            ClaudeStreamCompleted done => Task.FromResult<AgentEntry>(new AgentStreamCompleted(done.Sender)),
            ClaudeEfferent outgoing => Task.FromResult<AgentEntry>(new AgentAck(outgoing.Sender, Reagent)),
            ClaudeAck => Task.FromResult<AgentEntry>(new AgentAck(Input.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<ClaudeEntry> Transmute(AgentEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            AgentPrompt prompt => Task.FromResult<ClaudeEntry>(new ClaudeEfferent(prompt.Sender, prompt.Text)),

            AgentResponse response => Task.FromResult<ClaudeEntry>(new ClaudeAck(response.Sender, Reagent)),
            AgentThought thought => Task.FromResult<ClaudeEntry>(new ClaudeAck(thought.Sender, Reagent)),
            AgentEfferentChunk efferentChunk => Task.FromResult<ClaudeEntry>(new ClaudeAck(efferentChunk.Sender, Reagent)),
            AgentAfferentChunk afferentChunk => Task.FromResult<ClaudeEntry>(new ClaudeAck(afferentChunk.Sender, Reagent)),
            AgentEfferentThoughtChunk thoughtChunk => Task.FromResult<ClaudeEntry>(new ClaudeAck(thoughtChunk.Sender, Reagent)),
            AgentAfferentThoughtChunk afferentThought => Task.FromResult<ClaudeEntry>(new ClaudeAck(afferentThought.Sender, Reagent)),
            AgentStreamCompleted done => Task.FromResult<ClaudeEntry>(new ClaudeAck(done.Sender, Reagent)),
            AgentAck ack => Task.FromResult<ClaudeEntry>(new ClaudeAck(ack.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
