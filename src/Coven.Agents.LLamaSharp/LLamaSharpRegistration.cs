// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Optional registration customizations for LLamaSharp agents.
/// </summary>
public sealed class LLamaSharpRegistration
{
    /// <summary>Gets a value indicating whether streaming is enabled.</summary>
    public bool StreamingEnabled { get; private set; }

    /// <summary>Enables streaming token-by-token responses from the local model.</summary>
    /// <returns>The same registration for fluent chaining.</returns>
    public LLamaSharpRegistration EnableStreaming()
    {
        StreamingEnabled = true;
        return this;
    }
}
