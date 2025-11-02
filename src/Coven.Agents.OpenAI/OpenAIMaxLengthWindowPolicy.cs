// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Emits when the recent OpenAI chunk(s) length reaches a max.
/// Minimal lookback of 1; intended as a safety cap in combination with semantic policies.
/// </summary>
internal sealed class OpenAIMaxLengthWindowPolicy : IWindowPolicy<OpenAIAfferentChunk>
{
    private readonly int _max;

    public OpenAIMaxLengthWindowPolicy(int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, 0);
        _max = max;
    }

    public int MinChunkLookback => 1;

    public bool ShouldEmit(StreamWindow<OpenAIAfferentChunk> window)
    {
        int total = 0;
        foreach (OpenAIAfferentChunk chunk in window.PendingChunks)
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

