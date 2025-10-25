// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming;

public interface IStreamSegmenter
{
    int MinChunkLookback { get; }

    bool ShouldEmit(StreamWindow window);
}

