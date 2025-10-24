// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming.Segmenters;

public sealed class ComposedSegmenter(params IStreamSegmenter[] segmenters) : IStreamSegmenter
{
    private readonly IStreamSegmenter[] _segmenters = segmenters?.Length > 0 ? segmenters : throw new ArgumentNullException(nameof(segmenters));

    public int MinChunkLookback
    {
        get
        {
            int max = 1;
            foreach (IStreamSegmenter s in _segmenters)
            {
                if (s.MinChunkLookback > max)
                {
                    max = s.MinChunkLookback;
                }
            }
            return max;
        }
    }

    public bool ShouldEmit(StreamWindow window)
    {
        foreach (IStreamSegmenter s in _segmenters)
        {
            if (s.ShouldEmit(window))
            {
                return true;
            }
        }
        return false;
    }
}

