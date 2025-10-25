// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Chat.Windowing;

/// <summary>
/// Emits when the concatenated chat text ends at a sentence boundary.
/// A sentence boundary is a trailing '.', '!' or '?' (ignoring trailing whitespace).
/// </summary>
public sealed class ChatSentenceWindowPolicy : IWindowPolicy<ChatChunk>
{
    // 4 chunks should be generous for windowing sentence termination
    public int MinChunkLookback => 4;

    public bool ShouldEmit(StreamWindow<ChatChunk> window)
    {
        StringBuilder sb = new();
        foreach (ChatChunk chunk in window.PendingChunks)
        {
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                sb.Append(chunk.Text);
            }
        }

        return sb.Length != 0 && EndsWithSentenceBoundary(sb);
    }

    private static bool EndsWithSentenceBoundary(StringBuilder sb)
    {
        int i = sb.Length - 1;
        while (i >= 0 && char.IsWhiteSpace(sb[i]))
        {
            i--;
        }


        if (i < 0)
        {
            return false;
        }


        char c = sb[i];
        return c is '.' or '!' or '?';
    }
}

