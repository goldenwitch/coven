// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core.Streaming;

namespace Coven.Chat.Shattering;

/// <summary>
/// Shatters drafts on paragraph boundaries.
/// - ChatIncomingDraft -> yields ChatChunk segments per paragraph
/// - ChatOutgoingDraft -> yields ChatChunk segments per paragraph
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
            case ChatEfferentDraft outgoingDraft:
                foreach (string part in SplitParagraphs(outgoingDraft.Text))
                {
                    yield return new ChatChunk(outgoingDraft.Sender, part);
                }
                yield break;
            case ChatAfferentDraft incomingDraft:
                foreach (string part in SplitParagraphs(incomingDraft.Text))
                {
                    yield return new ChatChunk(incomingDraft.Sender, part);
                }
                yield break;
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
