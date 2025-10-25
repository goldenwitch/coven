// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Chat.Shattering;

/// <summary>
/// Scatters Chat entries on paragraph boundaries.
/// - ChatIncoming -> yields ChatChunk segments per paragraph
/// - ChatOutgoing -> yields ChatOutgoing segments per paragraph
///
/// A paragraph boundary is a double newline sequence ("\r\n\r\n" or "\n\n").
/// The boundary newlines are preserved with the preceding paragraph to maintain formatting.
/// </summary>
public sealed class ChatParagraphShatterPolicy : IShatterPolicy<ChatEntry>
{
    public IEnumerable<ChatEntry> Shatter(ChatEntry entry)
    {
        switch (entry)
        {
            case ChatIncoming incoming:
                {
                    foreach (string part in SplitParagraphs(incoming.Text))
                    {
                        yield return new ChatChunk(incoming.Sender, part);
                    }
                    break;
                }
            case ChatOutgoing outgoing:
                {
                    foreach (string part in SplitParagraphs(outgoing.Text))
                    {
                        yield return new ChatOutgoing(outgoing.Sender, part);
                    }
                    break;
                }
            default:
                yield break;
        }
    }

    private static IEnumerable<string> SplitParagraphs(string? text)
    {
        string s = text ?? string.Empty;
        if (s.Length == 0)
        {
            yield break;
        }

        StringBuilder sb = new();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // Detect CRLF CRLF first
            if (c == '\r' && i + 3 < s.Length && s[i + 1] == '\n' && s[i + 2] == '\r' && s[i + 3] == '\n')
            {
                sb.Append("\r\n\r\n");
                i += 3;
                yield return sb.ToString();
                sb.Clear();
                continue;
            }

            // Detect LF LF
            if (c == '\n' && i + 1 < s.Length && s[i + 1] == '\n')
            {
                sb.Append("\n\n");
                i += 1;
                yield return sb.ToString();
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }
}
