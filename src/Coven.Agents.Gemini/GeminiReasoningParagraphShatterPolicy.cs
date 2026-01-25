// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Agents.Gemini;

/// <summary>
/// Shatters Gemini reasoning chunks at the first paragraph boundary to reduce window pressure.
/// Boundary is a double newline sequence ("\r\n\r\n" or "\n\n").
/// </summary>
internal sealed class GeminiReasoningParagraphShatterPolicy : IShatterPolicy<GeminiEntry>
{
    public IEnumerable<GeminiEntry> Shatter(GeminiEntry entry)
    {
        if (entry is not GeminiAfferentReasoningChunk chunk || string.IsNullOrEmpty(chunk.Text))
        {
            yield break;
        }

        string text = chunk.Text;

        int boundaryIndex = IndexOfParagraphBoundary(text, out int boundaryLength);
        if (boundaryIndex < 0)
        {
            yield break;
        }

        int splitAfter = boundaryIndex + boundaryLength;
        string first = text[..splitAfter];
        string second = text[splitAfter..];

        if (first.Length > 0)
        {
            yield return new GeminiAfferentReasoningChunk(chunk.Sender, first, chunk.ResponseId, chunk.Timestamp, chunk.Model);
        }
        if (second.Length > 0)
        {
            yield return new GeminiAfferentReasoningChunk(chunk.Sender, second, chunk.ResponseId, chunk.Timestamp, chunk.Model);
        }
    }

    private static int IndexOfParagraphBoundary(string s, out int boundaryLength)
    {
        boundaryLength = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\r' && i + 3 < s.Length && s[i + 1] == '\n' && s[i + 2] == '\r' && s[i + 3] == '\n')
            {
                boundaryLength = 4;
                return i;
            }
            if (c == '\n' && i + 1 < s.Length && s[i + 1] == '\n')
            {
                boundaryLength = 2;
                return i;
            }
        }
        return -1;
    }
}
