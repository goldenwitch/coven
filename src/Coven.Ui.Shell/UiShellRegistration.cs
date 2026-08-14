// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Ui.Shell;

/// <summary>
/// Registration options for the application shell journal.
/// </summary>
public sealed class UiShellRegistration
{
    /// <summary>
    /// Gets a value indicating whether the shell surfaces agent reasoning.
    /// </summary>
    public bool ReasoningEnabled { get; private set; }

    /// <summary>
    /// Declares that the shell consumes <see cref="UiThought"/>, so the covenant must route
    /// reasoning into it.
    /// </summary>
    /// <remarks>
    /// Off by default because reasoning is not universal: hosted providers emit an
    /// <c>AgentThought</c> entry, but a local GGUF model has no separate reasoning channel and
    /// its branch declares no such type. Enabling this against a provider that cannot produce
    /// reasoning fails covenant validation rather than silently rendering an empty pane.
    /// </remarks>
    /// <returns>The same registration for chaining.</returns>
    public UiShellRegistration EnableReasoning()
    {
        ReasoningEnabled = true;
        return this;
    }
}
