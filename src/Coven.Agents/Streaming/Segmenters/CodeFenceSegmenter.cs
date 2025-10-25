// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming.Segmenters;

public sealed class CodeFenceSegmenter : IStreamSegmenter
{
    // To correctly determine fence parity since last emission, we need to scan all pending chunks.
    public int MinChunkLookback => int.MaxValue;

    public bool ShouldEmit(StreamWindow window)
    {
        int backtickRun = 0;
        bool insideFence = false;

        foreach (string chunk in window.PendingChunks)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            foreach (char c in chunk)
            {
                if (c == '`')
                {
                    backtickRun++;
                    if (backtickRun == 3)
                    {
                        insideFence = !insideFence; // toggle on triple backticks
                        backtickRun = 0;
                    }
                }
                else
                {
                    backtickRun = 0;
                }
            }
        }

        // Emit only when not inside a code fence.
        return !insideFence;
    }
}
