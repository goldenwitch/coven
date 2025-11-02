// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Reasoning configuration for models that support structured reasoning controls.
/// </summary>
public sealed class ReasoningConfig
{
    /// <summary>
    /// Reasoning effort setting; defaults to <see cref="ReasoningEffort.Medium"/>.
    /// </summary>
    public ReasoningEffort Effort { get; init; } = ReasoningEffort.Medium;

    /// <summary>
    /// Controls verbosity of returned reasoning summaries.
    /// </summary>
    public ReasoningSummaryVerbosity SummaryVerbosity { get; init; } = ReasoningSummaryVerbosity.Auto;
}
