// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Emits when recent agent chunk length reaches a max. Minimal lookback of 1.
/// </summary>
public sealed class AgentMaxLengthWindowPolicy : IWindowPolicy<AgentAfferentChunk>
{
    private readonly int _max;

    public AgentMaxLengthWindowPolicy(int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, 0);
        _max = max;
    }

    public int MinChunkLookback => 1;

    public bool ShouldEmit(StreamWindow<AgentAfferentChunk> window)
    {
        int total = 0;
        foreach (AgentAfferentChunk chunk in window.PendingChunks)
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
