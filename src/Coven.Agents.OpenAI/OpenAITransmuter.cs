// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.OpenAI;

public sealed class OpenAITransmuter : IBiDirectionalTransmuter<OpenAIEntry, AgentEntry>
{
    public Task<AgentEntry> TransmuteAfferent(OpenAIEntry Input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIAfferent incoming => Task.FromResult<AgentEntry>(new AgentResponse(incoming.Sender, incoming.Text)),
            OpenAIAfferentChunk chunk => Task.FromResult<AgentEntry>(new AgentEfferentChunk(chunk.Sender, chunk.Text)),
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
            AgentStreamCompleted done => Task.FromResult<OpenAIEntry>(new OpenAIAck(done.Sender, string.Empty)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }
}
