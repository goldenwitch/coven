// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Maps between LLamaSharp-specific entries and generic Agent entries using position-imbued ACKs.
/// Afferent: LLamaSharp → Agent; Efferent: Agent → LLamaSharp.
/// </summary>
internal sealed class LLamaSharpTransmuter
    : IImbuingTransmuter<LLamaSharpEntry, long, AgentEntry>,
      IImbuingTransmuter<AgentEntry, long, LLamaSharpEntry>
{
    public Task<AgentEntry> Transmute(LLamaSharpEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            LLamaSharpAfferent incoming => Task.FromResult<AgentEntry>(new AgentResponse(incoming.Sender, incoming.Text)),
            LLamaSharpAfferentChunk chunk => Task.FromResult<AgentEntry>(new AgentAfferentChunk(chunk.Sender, chunk.Text)),
            LLamaSharpStreamCompleted done => Task.FromResult<AgentEntry>(new AgentStreamCompleted(done.Sender)),
            LLamaSharpEfferent outgoing => Task.FromResult<AgentEntry>(new AgentAck(outgoing.Sender, Reagent)),
            LLamaSharpAck => Task.FromResult<AgentEntry>(new AgentAck(Input.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<LLamaSharpEntry> Transmute(AgentEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            AgentPrompt prompt => Task.FromResult<LLamaSharpEntry>(new LLamaSharpEfferent(prompt.Sender, prompt.Text)),

            AgentResponse response => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(response.Sender, Reagent)),
            AgentThought thought => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(thought.Sender, Reagent)),
            AgentEfferentChunk efferentChunk => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(efferentChunk.Sender, Reagent)),
            AgentAfferentChunk afferentChunk => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(afferentChunk.Sender, Reagent)),
            AgentEfferentThoughtChunk thoughtChunk => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(thoughtChunk.Sender, Reagent)),
            AgentAfferentThoughtChunk afferentThought => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(afferentThought.Sender, Reagent)),
            AgentStreamCompleted done => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(done.Sender, Reagent)),
            AgentAck ack => Task.FromResult<LLamaSharpEntry>(new LLamaSharpAck(ack.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
