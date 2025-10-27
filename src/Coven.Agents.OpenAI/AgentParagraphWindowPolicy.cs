// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Emits agent output when the recent text (last 1–2 chunks) ends at a paragraph boundary.
/// Uses a minimal lookback of 2 to handle boundaries that straddle chunk edges.
/// </summary>
public sealed class AgentParagraphWindowPolicy : IWindowPolicy<AgentAfferentChunk>
{
    public int MinChunkLookback => 2;

    public bool ShouldEmit(StreamWindow<AgentAfferentChunk> window)
    {
        StringBuilder sb = new();
        foreach (AgentAfferentChunk chunk in window.PendingChunks)
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
