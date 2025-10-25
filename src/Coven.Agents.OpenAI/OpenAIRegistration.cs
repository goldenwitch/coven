// SPDX-License-Identifier: BUSL-1.1
using Coven.Agents;

namespace Coven.Agents.OpenAI;

public sealed class OpenAIRegistration
{
    internal bool StreamingEnabled { get; private set; }

    public OpenAIRegistration EnableStreaming()
    {
        StreamingEnabled = true;
        return this;
    }
}
