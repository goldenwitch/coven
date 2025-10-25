// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

public interface IStreamSegmenter<TChunk>
{
    int MinChunkLookback { get; }

    bool ShouldEmit(StreamWindow<TChunk> window);
}

