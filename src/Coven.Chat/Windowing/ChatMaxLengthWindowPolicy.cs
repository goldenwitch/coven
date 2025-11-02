// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Chat.Windowing;

/// <summary>
/// Emits when the concatenated chat text length reaches or exceeds a max.
/// Intended to be composed with other semantic window policies via CompositeWindowPolicy.
/// </summary>
public sealed class ChatMaxLengthWindowPolicy : IWindowPolicy<ChatChunk>
{
    private readonly int _max;

    /// <summary>
    /// Creates a policy that emits when the total length of recent chat chunks reaches the maximum.
    /// </summary>
    /// <param name="max">Maximum total characters across the current window; must be greater than zero.</param>
    public ChatMaxLengthWindowPolicy(int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, 0);
        _max = max;
    }

    /// <inheritdoc />
    public int MinChunkLookback => int.MaxValue;

    /// <inheritdoc />
    public bool ShouldEmit(StreamWindow<ChatChunk> window)
    {
        int total = 0;
        foreach (ChatChunk chunk in window.PendingChunks)
        {
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                total += chunk.Text.Length;
                if (total >= _max)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
