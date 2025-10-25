// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Chat.Windowing;

/// <summary>
/// Emits when the concatenated chat text ends at a paragraph boundary
/// (double newline sequences like "\r\n\r\n" or "\n\n").
/// </summary>
public sealed class ChatParagraphWindowPolicy : IWindowPolicy<ChatChunk>
{
    public int MinChunkLookback => int.MaxValue;

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

        if (sb.Length == 0)
        {
            return false;
        }

        // Allow trailing spaces/tabs before assessing paragraph boundary
        string s = TrimEndExceptNewlines(sb.ToString());
        return s.EndsWith("\r\n\r\n", StringComparison.Ordinal) || s.EndsWith("\n\n", StringComparison.Ordinal);
    }

    private static string TrimEndExceptNewlines(string s)
    {
        int end = s.Length;
        while (end > 0)
        {
            char c = s[end - 1];
            if (c is ' ' or '\t')
            {
                end--;
            }
            else
            {
                break;
            }

        }
        return end == s.Length ? s : s[..end];
    }
}

