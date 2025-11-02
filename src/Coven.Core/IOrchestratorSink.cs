// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// Orchestrator-facing completion callbacks for Pull mode.
/// </summary>
public interface IOrchestratorSink
{
    /// <summary>
    /// Called when a single step completes; output type is preserved generically.
    /// </summary>
    void Complete<TOut>(TOut output, string? branchId = null);

    /// <summary>
    /// Called when the Board reaches the final output type for the current ritual.
    /// </summary>
    void CompletedFinal<TFinal>(TFinal result);
}
