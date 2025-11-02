// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Emits when the concatenated OpenAI text (last 1–2 chunks) ends at a paragraph boundary.
/// A paragraph boundary is a double newline sequence ("\r\n\r\n" or "\n\n").
/// Uses a minimal lookback of 2 to account for boundaries that straddle chunk edges.
/// </summary>
internal sealed class OpenAIParagraphWindowPolicy : IWindowPolicy<OpenAIAfferentChunk>
{
    public int MinChunkLookback => 2;

    public bool ShouldEmit(StreamWindow<OpenAIAfferentChunk> window)
    {
        StringBuilder sb = new();
        foreach (OpenAIAfferentChunk chunk in window.PendingChunks)
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

