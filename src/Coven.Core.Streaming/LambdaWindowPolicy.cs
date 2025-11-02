// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Simple window policy backed by a delegate and a fixed minimum lookback.
/// </summary>
public sealed class LambdaWindowPolicy<TChunk> : IWindowPolicy<TChunk>
{
    private readonly Func<StreamWindow<TChunk>, bool> _shouldEmit;
    /// <inheritdoc />
    public int MinChunkLookback { get; }

    /// <summary>
    /// Creates a new policy.
    /// </summary>
    /// <param name="minLookback">Minimum number of recent chunks to consider (at least 1).</param>
    /// <param name="shouldEmit">Delegate that determines whether to emit for a given window.</param>
    public LambdaWindowPolicy(int minLookback, Func<StreamWindow<TChunk>, bool> shouldEmit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minLookback, 1);
        _shouldEmit = shouldEmit ?? throw new ArgumentNullException(nameof(shouldEmit));
        MinChunkLookback = minLookback;
    }

    /// <inheritdoc />
    public bool ShouldEmit(StreamWindow<TChunk> window) => _shouldEmit(window);
}
