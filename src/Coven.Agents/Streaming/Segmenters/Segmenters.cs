// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming.Segmenters;

public static class Segmenters
{
    public static IStreamSegmenter SentenceBoundary(int minLen = 0) => new SentenceBoundarySegmenter(minLen);

    public static IStreamSegmenter DoubleNewline() => new DoubleNewlineSegmenter();

    public static IStreamSegmenter CodeFence() => new CodeFenceSegmenter();

    public static IStreamSegmenter FinalOnly() => new FinalOnlySegmenter();

    public static IStreamSegmenter Compose(params IStreamSegmenter[] segmenters) => new ComposedSegmenter(segmenters);
}

