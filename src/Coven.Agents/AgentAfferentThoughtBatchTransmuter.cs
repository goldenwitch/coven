// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;
using Coven.Transmutation;

namespace Coven.Agents;

public sealed class AgentAfferentThoughtBatchTransmuter : ITransmuter<IEnumerable<AgentAfferentThoughtChunk>, BatchTransmuteResult<AgentAfferentThoughtChunk, AgentThought>>
{
    public Task<BatchTransmuteResult<AgentAfferentThoughtChunk, AgentThought>> Transmute(IEnumerable<AgentAfferentThoughtChunk> Input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Input);

        string sender = string.Empty;
        StringBuilder sb = new();
        foreach (AgentAfferentThoughtChunk chunk in Input)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(chunk.Sender))
            {
                sender = chunk.Sender;
            }
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                sb.Append(chunk.Text);
            }
        }

        AgentThought output = new(sender, sb.ToString());
        return Task.FromResult(new BatchTransmuteResult<AgentAfferentThoughtChunk, AgentThought>(output, false, null));
    }
}

