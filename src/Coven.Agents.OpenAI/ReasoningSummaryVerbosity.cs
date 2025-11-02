// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Controls verbosity of model-provided reasoning summaries when available.
/// </summary>
public enum ReasoningSummaryVerbosity
{
    /// <summary>Let the model decide verbosity.</summary>
    Auto,
    /// <summary>Prefer brief summaries.</summary>
    Concise,
    /// <summary>Prefer detailed summaries.</summary>
    Detailed
}
