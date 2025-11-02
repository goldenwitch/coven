// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Emits when the concatenated thought text ends at a sentence boundary.
/// A sentence boundary is a trailing '.', '!' or '?' (ignoring trailing whitespace).
/// </summary>
public sealed class AgentThoughtSentenceWindowPolicy : IWindowPolicy<AgentAfferentThoughtChunk>
{
    // 4 chunks should be generous for windowing sentence termination
    /// <inheritdoc />
    public int MinChunkLookback => 4;

    /// <inheritdoc />
    public bool ShouldEmit(StreamWindow<AgentAfferentThoughtChunk> window)
    {
        StringBuilder sb = new();
        foreach (AgentAfferentThoughtChunk chunk in window.PendingChunks)
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
