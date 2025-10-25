// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;
using Coven.Transmutation;

namespace Coven.Agents;

public sealed class AgentChunkBatchTransmuter : ITransmuter<IEnumerable<AgentChunk>, BatchTransmuteResult<AgentChunk, AgentResponse>>
{
    public Task<BatchTransmuteResult<AgentChunk, AgentResponse>> Transmute(IEnumerable<AgentChunk> Input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Input);

        string sender = string.Empty;
        StringBuilder sb = new();
        foreach (AgentChunk chunk in Input)
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

        AgentResponse output = new(sender, sb.ToString());
        return Task.FromResult(new BatchTransmuteResult<AgentChunk, AgentResponse>(output, false, null));
    }
}
