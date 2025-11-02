// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Transmutation;

namespace Coven.Chat;

/// <summary>
/// Batches chat chunks into a single <see cref="ChatEfferent"/> message.
/// </summary>
public sealed class ChatChunkBatchTransmuter : IBatchTransmuter<ChatChunk, ChatEfferent>
{
    /// <inheritdoc />
    public Task<BatchTransmuteResult<ChatChunk, ChatEfferent>> Transmute(IEnumerable<ChatChunk> Input, CancellationToken cancellationToken = default)
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

        ChatEfferent output = new(sender, sb.ToString());
        return Task.FromResult(new BatchTransmuteResult<ChatChunk, ChatEfferent>(output, false, null));
    }
}
