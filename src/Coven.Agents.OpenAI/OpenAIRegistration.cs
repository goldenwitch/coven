// SPDX-License-Identifier: BUSL-1.1
using Coven.Agents.Streaming;
using Coven.Agents.Streaming.Segmenters;

namespace Coven.Agents.OpenAI;

public sealed class OpenAIRegistration
{
    internal bool StreamingEnabled { get; private set; }
    internal IStreamSegmenter? Segmenter { get; private set; }

    public OpenAIRegistration EnableStreaming(IStreamSegmenter? segmenter = null)
    {
        StreamingEnabled = true;
        Segmenter = segmenter ?? Segmenters.FinalOnly();
        return this;
    }
}

