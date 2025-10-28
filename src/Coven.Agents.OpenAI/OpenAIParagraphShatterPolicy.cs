// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Shatters OpenAI afferent chunks at the first paragraph boundary.
/// A paragraph boundary is a double newline sequence ("\r\n\r\n" or "\n\n").
/// When a boundary is found, emits exactly two chunks:
/// - First: original text up to and including the boundary
/// - Second: remainder of the original text
/// If no boundary exists, produces no outputs (forward unchanged).
/// </summary>
public sealed class OpenAIParagraphShatterPolicy : IShatterPolicy<OpenAIEntry>
{
    public IEnumerable<OpenAIEntry> Shatter(OpenAIEntry entry)
    {
        if (entry is not OpenAIAfferentChunk chunk || string.IsNullOrEmpty(chunk.Text))
        {
            yield break;
        }

        string text = chunk.Text;

        // Prefer CRLF CRLF over LF LF when both could match starting at same position
        int boundaryIndex = IndexOfParagraphBoundary(text, out int boundaryLength);
        if (boundaryIndex < 0)
        {
            yield break; // no change
        }

        int splitAfter = boundaryIndex + boundaryLength;
        string first = text[..splitAfter];
        string second = text[splitAfter..];

        if (first.Length > 0)
        {
            yield return new OpenAIAfferentChunk(chunk.Sender, first, chunk.ResponseId, chunk.Timestamp, chunk.Model);
        }
        if (second.Length > 0)
        {
            yield return new OpenAIAfferentChunk(chunk.Sender, second, chunk.ResponseId, chunk.Timestamp, chunk.Model);
        }
    }

    private static int IndexOfParagraphBoundary(string s, out int boundaryLength)
    {
        boundaryLength = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // CRLF CRLF
            if (c == '\r' && i + 3 < s.Length && s[i + 1] == '\n' && s[i + 2] == '\r' && s[i + 3] == '\n')
            {
                boundaryLength = 4;
                return i;
            }
            // LF LF
            if (c == '\n' && i + 1 < s.Length && s[i + 1] == '\n')
            {
                boundaryLength = 2;
                return i;
            }
        }
        return -1;
    }
}
