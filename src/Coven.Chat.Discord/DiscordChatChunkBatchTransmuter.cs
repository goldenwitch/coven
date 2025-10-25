// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;
using Coven.Transmutation;

namespace Coven.Chat.Discord;

public sealed class DiscordChatChunkBatchTransmuter : ITransmuter<IEnumerable<ChatChunk>, BatchTransmuteResult<ChatChunk, ChatOutgoing>>
{
    private const int Max = 2000;

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

        string text = sb.ToString();
        if (text.Length <= Max)
        {
            ChatOutgoing full = new(sender, text);
            return Task.FromResult(new BatchTransmuteResult<ChatChunk, ChatOutgoing>(full, false, null));
        }

        // Upstream segmentation should end on sentence boundaries.
        // Here we only enforce Discord's 2k char limit without extra heuristics.
        int cut = Math.Min(Max, text.Length);
        string head = text[..cut];
        string tail = text[cut..];

        ChatOutgoing output = new(sender, head);
        ChatChunk? remainder = string.IsNullOrEmpty(tail) ? null : new ChatChunk(sender, tail);
        bool hasRemainder = remainder is not null;

        return Task.FromResult(new BatchTransmuteResult<ChatChunk, ChatOutgoing>(output, hasRemainder, remainder));
    }
}
