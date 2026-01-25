// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Agents;

/// <summary>
/// Emits when recent agent chunk length reaches a max. Minimal lookback of 1.
/// </summary>
public sealed class AgentMaxLengthWindowPolicy : IWindowPolicy<AgentAfferentChunk>
{
    private readonly int _max;

    /// <summary>
    /// Creates a policy that emits when the total length of recent chunks reaches the maximum.
    /// </summary>
    /// <param name="max">Maximum total characters across the current window; must be greater than zero.</param>
    public AgentMaxLengthWindowPolicy(int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, 0);
        _max = max;
    }

    /// <inheritdoc />
    public int MinChunkLookback => 1;

    /// <inheritdoc />
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
