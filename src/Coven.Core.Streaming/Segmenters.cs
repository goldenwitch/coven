// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

public static class Segmenters
{
    public static IStreamSegmenter<TChunk> FinalOnly<TChunk>() => new LambdaSegmenter<TChunk>(1, _ => false);
}

