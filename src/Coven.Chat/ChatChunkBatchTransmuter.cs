// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;
using Coven.Transmutation;

namespace Coven.Chat;

public sealed class ChatChunkBatchTransmuter : ITransmuter<IEnumerable<ChatChunk>, BatchTransmuteResult<ChatChunk, ChatOutgoing>>
{
    public Task<BatchTransmuteResult<ChatChunk, ChatOutgoing>> Transmute(IEnumerable<ChatChunk> Input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Input);

        string sender = string.Empty;
        StringBuilder sb = new();
        foreach (ChatChunk chunk in Input)
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

        ChatOutgoing output = new(sender, sb.ToString());
        return Task.FromResult(new BatchTransmuteResult<ChatChunk, ChatOutgoing>(output, false, null));
    }
}
