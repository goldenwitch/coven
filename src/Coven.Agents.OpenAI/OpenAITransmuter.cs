// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAITransmuter : IBiDirectionalTransmuter<OpenAIEntry, AgentEntry>
{
    public Task<AgentEntry> TransmuteAfferent(OpenAIEntry Input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIAfferent incoming => Task.FromResult<AgentEntry>(new AgentResponse(incoming.Sender, incoming.Text)),
            // Today, Afferent chunks include thoughts, tomorrow who knows.
            OpenAIAfferentChunk chunk => Task.FromResult<AgentEntry>(new AgentAfferentChunk(chunk.Sender, chunk.Text)),
            // Streaming thought chunks from OpenAI surface as afferent thought drafts
            OpenAIAfferentThoughtChunk tChunk => Task.FromResult<AgentEntry>(new AgentAfferentThoughtChunk(tChunk.Sender, tChunk.Text)),
            // A full OpenAIThought should surface as a fixed AgentThought
            OpenAIThought thought => Task.FromResult<AgentEntry>(new AgentThought(thought.Sender, thought.Text)),
            OpenAIStreamCompleted done => Task.FromResult<AgentEntry>(new AgentStreamCompleted(done.Sender)),
            OpenAIEfferent outgoing => Task.FromResult<AgentEntry>(new AgentAck(outgoing.Sender)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<OpenAIEntry> TransmuteEfferent(AgentEntry Output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Output switch
        {
            AgentPrompt prompt => Task.FromResult<OpenAIEntry>(new OpenAIEfferent(prompt.Sender, prompt.Text)),
            AgentResponse response => Task.FromResult<OpenAIEntry>(new OpenAIAck(response.Sender, response.Text)),
            AgentThought thought => Task.FromResult<OpenAIEntry>(new OpenAIAck(thought.Sender, thought.Text)),
            AgentEfferentChunk affChunk => Task.FromResult<OpenAIEntry>(new OpenAIAck(affChunk.Sender, affChunk.Text)),
            AgentAfferentChunk effChunk => Task.FromResult<OpenAIEntry>(new OpenAIAck(effChunk.Sender, effChunk.Text)),
            // Streaming efferent thought drafts map to OpenAI efferent thought chunk (not forwarded by gateway today)
            AgentEfferentThoughtChunk etChunk => Task.FromResult<OpenAIEntry>(new OpenAIEfferentThoughtChunk(etChunk.Sender, etChunk.Text)),
            // Afferent thought drafts are not sent outward; ack for completeness
            AgentAfferentThoughtChunk atChunk => Task.FromResult<OpenAIEntry>(new OpenAIAck(atChunk.Sender, atChunk.Text)),
            AgentStreamCompleted done => Task.FromResult<OpenAIEntry>(new OpenAIAck(done.Sender, string.Empty)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }
}
