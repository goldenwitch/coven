// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Registration customization for the OpenAI integration.
/// </summary>
public sealed class OpenAIRegistration
{
    internal bool StreamingEnabled { get; private set; }

    /// <summary>
    /// Enables streaming mode (streaming gateway + windowing daemons).
    /// </summary>
    /// <returns>The same registration for fluent chaining.</returns>
    public OpenAIRegistration EnableStreaming()
    {
        StreamingEnabled = true;
        return this;
    }
}
