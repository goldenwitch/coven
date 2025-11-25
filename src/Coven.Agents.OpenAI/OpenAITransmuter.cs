// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Maps between OpenAI-specific entries and generic Agent entries using position-imbued ACKs.
/// Afferent: OpenAI → Agent; Efferent: Agent → OpenAI.
/// </summary>
internal sealed class OpenAITransmuter
    : IImbuingTransmuter<OpenAIEntry, long, AgentEntry>,
      IImbuingTransmuter<AgentEntry, long, OpenAIEntry>
{
    /// <summary>
    /// Transmutes OpenAI-afferent entries to Agent entries.
    /// </summary>
    /// <param name="Input">The source OpenAI entry.</param>
    /// <param name="Reagent">The source journal position used for position-based acknowledgements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mapped Agent entry.</returns>
    public Task<AgentEntry> Transmute(OpenAIEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIAfferent incoming => Task.FromResult<AgentEntry>(new AgentResponse(incoming.Sender, incoming.Text)),
            OpenAIAfferentChunk chunk => Task.FromResult<AgentEntry>(new AgentAfferentChunk(chunk.Sender, chunk.Text)),
            OpenAIAfferentThoughtChunk tChunk => Task.FromResult<AgentEntry>(new AgentAfferentThoughtChunk(tChunk.Sender, tChunk.Text)),
            OpenAIThought thought => Task.FromResult<AgentEntry>(new AgentThought(thought.Sender, thought.Text)),
            OpenAIStreamCompleted done => Task.FromResult<AgentEntry>(new AgentStreamCompleted(done.Sender)),
            // For efferent records observed on the OpenAI journal or explicit OpenAI acks, emit an AgentAck with the source position
            OpenAIEfferent outgoing => Task.FromResult<AgentEntry>(new AgentAck(outgoing.Sender, Reagent)),
            OpenAIAck => Task.FromResult<AgentEntry>(new AgentAck(Input.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    /// <summary>
    /// Transmutes Agent-efferent entries to OpenAI entries.
    /// </summary>
    /// <param name="Input">The source Agent entry.</param>
    /// <param name="Reagent">The source journal position used for position-based acknowledgements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mapped OpenAI entry.</returns>
    public Task<OpenAIEntry> Transmute(AgentEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            AgentPrompt prompt => Task.FromResult<OpenAIEntry>(new OpenAIEfferent(prompt.Sender, prompt.Text)),

            // All other Agent entries (including drafts and fixed) acknowledge with the position being processed
            AgentResponse response => Task.FromResult<OpenAIEntry>(new OpenAIAck(response.Sender, Reagent)),
            AgentThought thought => Task.FromResult<OpenAIEntry>(new OpenAIAck(thought.Sender, Reagent)),
            AgentEfferentChunk efferentChunk => Task.FromResult<OpenAIEntry>(new OpenAIAck(efferentChunk.Sender, Reagent)),
            AgentAfferentChunk afferentChunk => Task.FromResult<OpenAIEntry>(new OpenAIAck(afferentChunk.Sender, Reagent)),
            AgentEfferentThoughtChunk etChunk => Task.FromResult<OpenAIEntry>(new OpenAIEfferentThoughtChunk(etChunk.Sender, etChunk.Text)),
            AgentAfferentThoughtChunk atChunk => Task.FromResult<OpenAIEntry>(new OpenAIAck(atChunk.Sender, Reagent)),
            AgentStreamCompleted done => Task.FromResult<OpenAIEntry>(new OpenAIAck(done.Sender, Reagent)),
            AgentAck ack => Task.FromResult<OpenAIEntry>(new OpenAIAck(ack.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
