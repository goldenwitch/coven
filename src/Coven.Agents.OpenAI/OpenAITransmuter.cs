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
            OpenAIAfferentChunk chunk => Task.FromResult<AgentEntry>(new AgentChunk(chunk.Sender, chunk.Text)),
            OpenAIThought thought => Task.FromResult<AgentEntry>(new AgentThought(thought.Sender, thought.Text)),
            OpenAIStreamCompleted done => Task.FromResult<AgentEntry>(new AgentStreamCompleted(done.Sender)),
            OpenAIEfferent outgoing => Task.FromResult<AgentEntry>(new AgentAck(outgoing.Sender, outgoing.Text)),
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
            AgentChunk chunk => Task.FromResult<OpenAIEntry>(new OpenAIAck(chunk.Sender, chunk.Text)),
            AgentStreamCompleted done => Task.FromResult<OpenAIEntry>(new OpenAIAck(done.Sender, string.Empty)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }
}
