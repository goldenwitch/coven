// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

public sealed class LambdaSegmenter<TChunk> : IStreamSegmenter<TChunk>
{
    private readonly Func<StreamWindow<TChunk>, bool> _shouldEmit;
    public int MinChunkLookback { get; }

    public LambdaSegmenter(int minLookback, Func<StreamWindow<TChunk>, bool> shouldEmit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minLookback, 1);
        _shouldEmit = shouldEmit ?? throw new ArgumentNullException(nameof(shouldEmit));
        MinChunkLookback = minLookback;
    }

    public bool ShouldEmit(StreamWindow<TChunk> window) => _shouldEmit(window);
}

