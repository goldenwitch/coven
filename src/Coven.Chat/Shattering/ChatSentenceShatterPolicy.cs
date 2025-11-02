// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Chat.Shattering;

/// <summary>
/// Shatters drafts into sentence-sized segments.
/// - ChatIncomingDraft -> yields ChatChunk per sentence
/// - ChatOutgoingDraft -> yields ChatChunk per sentence
///
/// A sentence ends on '.', '!' or '?' followed by whitespace or end-of-input.
/// Trailing whitespace is preserved with the sentence it follows.
/// </summary>
public sealed class ChatSentenceShatterPolicy : IShatterPolicy<ChatEntry>
{
    /// <summary>
    /// Splits supported draft entries into sentence-sized <see cref="ChatChunk"/> segments.
    /// </summary>
    /// <param name="entry">The entry to consider for shattering.</param>
    /// <returns>
    /// Zero or more <see cref="ChatEntry"/> instances. For unsupported entry types, yields nothing.
    /// </returns>
    public IEnumerable<ChatEntry> Shatter(ChatEntry entry)
    {
        switch (entry)
        {
            case ChatEfferentDraft outgoingDraft:
                foreach (string s in SplitSentences(outgoingDraft.Text))
                {
                    yield return new ChatChunk(outgoingDraft.Sender, s);
                }
                yield break;
            case ChatAfferentDraft incomingDraft:
                foreach (string s in SplitSentences(incomingDraft.Text))
                {
                    yield return new ChatChunk(incomingDraft.Sender, s);
                }
                yield break;
            default:
                yield break;
        }
    }

    private static IEnumerable<string> SplitSentences(string? text)
    {
        string t = text ?? string.Empty;
        if (t.Length == 0)
        {
            yield break;
        }

        StringBuilder sb = new();
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            sb.Append(c);

            bool boundary = c is '.' or '!' or '?';
            if (!boundary)
            {
                continue;
            }

            // Lookahead: boundary if next is whitespace or end
            bool atEnd = i + 1 >= t.Length;
            bool nextIsWhitespace = !atEnd && char.IsWhiteSpace(t[i + 1]);
            if (atEnd || nextIsWhitespace)
            {
                yield return sb.ToString();
                sb.Clear();
                continue;
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }
}
