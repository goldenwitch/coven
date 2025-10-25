// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Chat.Shattering;

/// <summary>
/// Splits ChatOutgoing into multiple ChatOutgoing entries with a maximum length per entry.
/// Keeps text contiguous without extra heuristics.
/// </summary>
public sealed class ChatOutgoingMaxLengthShatterPolicy : IShatterPolicy<ChatEntry>
{
    private readonly int _max;

    public ChatOutgoingMaxLengthShatterPolicy(int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, 0);
        _max = max;
    }

    public IEnumerable<ChatEntry> Shatter(ChatEntry entry)
    {
        if (entry is not ChatOutgoing outgoing)
        {
            yield break;
        }

        string sender = outgoing.Sender;
        string text = outgoing.Text ?? string.Empty;
        if (text.Length <= _max)
        {
            yield break; // no new entries; original can be used as-is by the caller
        }

        for (int i = 0; i < text.Length; i += _max)
        {
            int len = Math.Min(_max, text.Length - i);
            yield return new ChatOutgoing(sender, text.Substring(i, len));
        }
    }
}
