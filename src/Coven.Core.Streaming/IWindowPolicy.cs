// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

public interface IWindowPolicy<TChunk>
{
    int MinChunkLookback { get; }

    bool ShouldEmit(StreamWindow<TChunk> window);
}

