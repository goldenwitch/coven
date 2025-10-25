// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming.Segmenters;

public sealed class FinalOnlySegmenter : IStreamSegmenter
{
    public int MinChunkLookback => 1;

    public bool ShouldEmit(StreamWindow window) => false;
}

