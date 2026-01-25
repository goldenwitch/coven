// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.Gemini;

/// <summary>
/// Maps between Gemini-specific entries and generic Agent entries using position-imbued ACKs.
/// Afferent: Gemini → Agent; Efferent: Agent → Gemini.
/// </summary>
internal sealed class GeminiTransmuter
    : IImbuingTransmuter<GeminiEntry, long, AgentEntry>,
      IImbuingTransmuter<AgentEntry, long, GeminiEntry>
{
    public Task<AgentEntry> Transmute(GeminiEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            GeminiAfferent incoming => Task.FromResult<AgentEntry>(new AgentResponse(incoming.Sender, incoming.Text)),
            GeminiAfferentChunk chunk => Task.FromResult<AgentEntry>(new AgentAfferentChunk(chunk.Sender, chunk.Text)),
            GeminiAfferentReasoningChunk reasoning => Task.FromResult<AgentEntry>(new AgentAfferentThoughtChunk(reasoning.Sender, reasoning.Text)),
            GeminiThought thought => Task.FromResult<AgentEntry>(new AgentThought(thought.Sender, thought.Text)),
            GeminiStreamCompleted done => Task.FromResult<AgentEntry>(new AgentStreamCompleted(done.Sender)),
            GeminiSafetyBlock safety => Task.FromResult<AgentEntry>(new AgentThought(safety.Sender, $"Gemini safety block: {safety.Reason}")),
            GeminiEfferent outgoing => Task.FromResult<AgentEntry>(new AgentAck(outgoing.Sender, Reagent)),
            GeminiAck => Task.FromResult<AgentEntry>(new AgentAck(Input.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<GeminiEntry> Transmute(AgentEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            AgentPrompt prompt => Task.FromResult<GeminiEntry>(new GeminiEfferent(prompt.Sender, prompt.Text)),

            AgentResponse response => Task.FromResult<GeminiEntry>(new GeminiAck(response.Sender, Reagent)),
            AgentThought thought => Task.FromResult<GeminiEntry>(new GeminiAck(thought.Sender, Reagent)),
            AgentEfferentChunk efferentChunk => Task.FromResult<GeminiEntry>(new GeminiAck(efferentChunk.Sender, Reagent)),
            AgentAfferentChunk afferentChunk => Task.FromResult<GeminiEntry>(new GeminiAck(afferentChunk.Sender, Reagent)),
            AgentEfferentThoughtChunk thoughtChunk => Task.FromResult<GeminiEntry>(new GeminiAck(thoughtChunk.Sender, Reagent)),
            AgentAfferentThoughtChunk afferentThought => Task.FromResult<GeminiEntry>(new GeminiAck(afferentThought.Sender, Reagent)),
            AgentStreamCompleted done => Task.FromResult<GeminiEntry>(new GeminiAck(done.Sender, Reagent)),
            AgentAck ack => Task.FromResult<GeminiEntry>(new GeminiAck(ack.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
