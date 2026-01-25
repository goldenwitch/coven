// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Gemini;

/// <summary>
/// Optional registration customizations for Gemini agents.
/// </summary>
public sealed class GeminiRegistration
{
    /// <summary>Gets a value indicating whether streaming is enabled.</summary>
    public bool StreamingEnabled { get; private set; }

    /// <summary>Enables streaming responses from Gemini.</summary>
    public void EnableStreaming() => StreamingEnabled = true;
}
