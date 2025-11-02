// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Emits agent thought output when the recent text (last 1–2 chunks) ends at a paragraph boundary.
/// Uses a minimal lookback of 2 to handle boundaries that straddle chunk edges.
/// </summary>
public sealed class AgentThoughtParagraphWindowPolicy : IWindowPolicy<AgentAfferentThoughtChunk>
{
    /// <inheritdoc />
    public int MinChunkLookback => 2;

    /// <inheritdoc />
    public bool ShouldEmit(StreamWindow<AgentAfferentThoughtChunk> window)
    {
        StringBuilder stringBuilder = new();
        foreach (AgentAfferentThoughtChunk chunk in window.PendingChunks)
        {
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                stringBuilder.Append(chunk.Text);
            }
        }

        if (stringBuilder.Length == 0)
        {
            return false;
        }

        string concatenatedWindow = stringBuilder.ToString();
        return concatenatedWindow.EndsWith("\r\n\r\n", StringComparison.Ordinal) || concatenatedWindow.EndsWith("\n\n", StringComparison.Ordinal);
    }
}
