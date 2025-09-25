// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

// Orchestrator-facing completion callbacks for Pull mode.
public interface IOrchestratorSink
{
    // Called when a single step completes; output type is preserved generically.
    void Complete<TOut>(TOut output, string? branchId = null);

    // Called when the Board reaches the final output type for the current Ritual.
    void CompletedFinal<TFinal>(TFinal result);
}
