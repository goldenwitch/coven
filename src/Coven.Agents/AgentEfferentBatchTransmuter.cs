// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Transmutation;

namespace Coven.Agents;

public sealed class AgentEfferentBatchTransmuter : IBatchTransmuter<AgentEfferentChunk, AgentPrompt>
{
    public Task<BatchTransmuteResult<AgentEfferentChunk, AgentPrompt>> Transmute(IEnumerable<AgentEfferentChunk> Input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Input);

        string sender = string.Empty;
        StringBuilder sb = new();
        foreach (AgentEfferentChunk chunk in Input)
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

        AgentPrompt output = new(sender, sb.ToString());
        return Task.FromResult(new BatchTransmuteResult<AgentEfferentChunk, AgentPrompt>(output, false, null));
    }
}
