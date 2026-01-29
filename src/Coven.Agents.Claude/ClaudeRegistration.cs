// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Claude;

/// <summary>
/// Optional registration customizations for Claude agents.
/// </summary>
public sealed class ClaudeRegistration
{
    /// <summary>Gets a value indicating whether streaming is enabled.</summary>
    public bool StreamingEnabled { get; private set; }

    /// <summary>Enables streaming responses from Claude.</summary>
    /// <returns>The same registration for fluent chaining.</returns>
    public ClaudeRegistration EnableStreaming()
    {
        StreamingEnabled = true;
        return this;
    }
}
