// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Chat.Shattering;

/// <summary>
/// Scatters Chat entries into sentence-sized segments.
/// - ChatIncoming -> yields ChatChunk per sentence
/// - ChatOutgoing -> yields ChatOutgoing per sentence
///
/// A sentence ends on '.', '!' or '?' followed by whitespace or end-of-input.
/// Trailing whitespace is preserved with the sentence it follows.
/// </summary>
public sealed class ChatSentenceShatterPolicy : IShatterPolicy<ChatEntry>
{
    public IEnumerable<ChatEntry> Shatter(ChatEntry entry)
    {
        switch (entry)
        {
            case ChatIncoming incoming:
                {
                    foreach (string s in SplitSentences(incoming.Text))
                    {
                        yield return new ChatChunk(incoming.Sender, s);
                    }
                    break;
                }
            case ChatOutgoing outgoing:
                {
                    foreach (string s in SplitSentences(outgoing.Text))
                    {
                        yield return new ChatOutgoing(outgoing.Sender, s);
                    }
                    break;
                }
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
