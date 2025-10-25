// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Chat.Shattering;

/// <summary>
/// Scatters a ChatIncoming into sentence-sized ChatChunk segments.
/// A sentence ends on '.', '!' or '?' followed by whitespace or end-of-input.
/// Trailing whitespace is preserved with the sentence it follows.
/// </summary>
public sealed class ChatSentenceShatterPolicy : IShatterPolicy<ChatIncoming, ChatChunk>
{
    public IEnumerable<ChatChunk> Shatter(ChatIncoming source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string text = source.Text ?? string.Empty;
        if (text.Length == 0)
        {
            yield break;
        }

        StringBuilder sb = new();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            sb.Append(c);

            bool boundary = c is '.' or '!' or '?';
            if (!boundary)
            {
                continue;
            }

            // Lookahead: boundary if next is whitespace or end
            bool atEnd = i + 1 >= text.Length;
            bool nextIsWhitespace = !atEnd && char.IsWhiteSpace(text[i + 1]);
            if (atEnd || nextIsWhitespace)
            {
                yield return new ChatChunk(source.Sender, sb.ToString());
                sb.Clear();
                continue;
            }
        }

        if (sb.Length > 0)
        {
            yield return new ChatChunk(source.Sender, sb.ToString());
        }
    }
}

