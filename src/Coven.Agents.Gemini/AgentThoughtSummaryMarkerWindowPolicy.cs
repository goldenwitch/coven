// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Agents.Gemini;

/// <summary>
/// Emits when a summary marker is observed in the thought stream.
/// The marker is any bold Markdown segment ("**...**") followed by a newline sequence.
/// Recognized sequences: "\n\n", "\r\n\r\n", or "\r\n".
/// </summary>
public sealed class AgentThoughtSummaryMarkerWindowPolicy : IWindowPolicy<AgentAfferentThoughtChunk>
{
    /// <inheritdoc />
    public int MinChunkLookback => 10;

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

        string text = stringBuilder.ToString();
        ReadOnlySpan<char> span = text.AsSpan();
        return HasBoldFollowedByNewline(span);
    }

    private static bool HasBoldFollowedByNewline(ReadOnlySpan<char> span)
    {
        int position = 0;
        while (position < span.Length)
        {
            int start = span[position..].IndexOf("**");
            if (start < 0)
            {
                return false;
            }
            start += position;

            int afterOpen = start + 2;
            if (afterOpen >= span.Length)
            {
                return false;
            }

            int end = span[afterOpen..].IndexOf("**");
            if (end < 0)
            {
                position = start + 2;
                continue;
            }
            end += afterOpen;

            if (end > start + 2)
            {
                int after = end + 2;
                ReadOnlySpan<char> tail = after <= span.Length ? span[after..] : [];

                if (tail.StartsWith("\r\n\r\n", StringComparison.Ordinal) ||
                    tail.StartsWith("\n\n", StringComparison.Ordinal) ||
                    tail.StartsWith("\r\n", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            position = end + 2;
        }

        return false;
    }
}
