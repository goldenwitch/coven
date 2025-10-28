// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

public sealed class ReasoningConfig
{
    // When reasoning is provided, assume enabled and default required settings.
    public ReasoningEffort Effort { get; init; } = ReasoningEffort.Medium;
    public ReasoningSummaryVerbosity SummaryVerbosity { get; init; } = ReasoningSummaryVerbosity.Auto;
}
