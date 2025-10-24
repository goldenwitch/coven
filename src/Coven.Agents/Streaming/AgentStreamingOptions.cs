// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming;

public sealed class AgentStreamingOptions
{
    public bool Enabled { get; set; }

    public IStreamSegmenter? Segmenter { get; set; }
}

