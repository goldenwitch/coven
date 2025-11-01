// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Chat.Shattering;

/// <summary>
/// Splits long chat text into <= max-length ChatChunk segments.
/// Applies to ChatOutgoingDraft and ChatChunk; other entries are ignored.
/// </summary>
public sealed class ChatChunkMaxLengthShatterPolicy : IShatterPolicy<ChatEntry>
{
    private readonly int _max;

    public ChatChunkMaxLengthShatterPolicy(int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, 0);
        _max = max;
    }

    public IEnumerable<ChatEntry> Shatter(ChatEntry entry)
    {
        switch (entry)
        {
            case ChatEfferentDraft draft:
                foreach (string part in Split(draft.Text))
                {
                    yield return new ChatChunk(draft.Sender, part);
                }
                yield break;
            case ChatChunk chunk when (chunk.Text?.Length ?? 0) > _max:
                foreach (string part in Split(chunk.Text))
                {
                    yield return new ChatChunk(chunk.Sender, part);
                }
                yield break;
            default:
                yield break;
        }
    }

    private IEnumerable<string> Split(string? text)
    {
        string s = text ?? string.Empty;
        if (s.Length <= _max)
        {
            if (s.Length > 0)
            {
                yield return s;
            }
            yield break;
        }

        for (int i = 0; i < s.Length; i += _max)
        {
            int len = Math.Min(_max, s.Length - i);
            yield return s.Substring(i, len);
        }
    }
}

