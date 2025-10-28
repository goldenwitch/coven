// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Emits when a summary marker is observed in the thought stream.
/// The marker is any bold Markdown segment ("**...**") followed by a newline sequence.
/// Recognized sequences: "\n\n", "\r\n\r\n", or "\r\n".
/// </summary>
public sealed class AgentThoughtSummaryMarkerWindowPolicy : IWindowPolicy<AgentAfferentThoughtChunk>
{
    public int MinChunkLookback => 1200;

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

        if (sb.Length == 0)
        {
            return false;
        }

        string text = sb.ToString();

        // Scan for any bold segment **...** followed immediately by a newline sequence
        int pos = 0;
        while (pos < text.Length)
        {
            int start = text.IndexOf("**", pos, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }
            int end = text.IndexOf("**", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                break; // no closing marker yet
            }
            // Require non-empty content between the markers
            if (end > start + 2)
            {
                int after = end + 2;
                if (after <= text.Length - 2)
                {
                    // Check for CRLF CRLF or LF LF
                    if ((after + 3 <= text.Length && text.AsSpan(after, 4).SequenceEqual("\r\n\r\n")) ||
                        (after + 1 <= text.Length && text.AsSpan(after, 2).SequenceEqual("\n\n")))
                    {
                        return true;
                    }
                }
                if (after + 1 <= text.Length && text.AsSpan(after, 2).SequenceEqual("\r\n"))
                {
                    return true;
                }
            }
            pos = end + 2; // continue searching after the closing marker
        }
        return false;
    }
}
