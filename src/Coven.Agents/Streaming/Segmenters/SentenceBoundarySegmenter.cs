// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming.Segmenters;

public sealed class SentenceBoundarySegmenter : IStreamSegmenter
{
    private readonly int _minLen;

    public SentenceBoundarySegmenter(int minLen = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minLen);
        _minLen = minLen;
    }

    // Sentence boundary rarely spans many chunks; 4 is conservative for tiny deltas.
    public int MinChunkLookback => 4;

    public bool ShouldEmit(StreamWindow window)
    {
        int totalLen = 0;
        char last = '\0';
        foreach (string chunk in window.PendingChunks)
        {
            totalLen = checked(totalLen + (chunk?.Length ?? 0));
            if (!string.IsNullOrEmpty(chunk))
            {
                last = chunk[^1];
            }
        }

        if (totalLen < _minLen)
        {
            return false;
        }

        // End-of-window implies end-of-chunk; consider boundary if terminal punctuation.
        return last is '.' or '!' or '?' or '。' or '！' or '？';
    }
}

