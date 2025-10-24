// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming.Segmenters;

public sealed class DoubleNewlineSegmenter : IStreamSegmenter
{
    public int MinChunkLookback => 2;

    public bool ShouldEmit(StreamWindow window)
    {
        bool prevNewline = false;
        foreach (string chunk in window.PendingChunks)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            foreach (char c in chunk)
            {
                if (c == '\n')
                {
                    if (prevNewline)
                    {
                        return true;
                    }
                    prevNewline = true;
                }
                else if (c == '\r')
                {
                    // Ignore; treat as part of CRLF
                }
                else
                {
                    prevNewline = false;
                }
            }
        }

        return false;
    }
}

