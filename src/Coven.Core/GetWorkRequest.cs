// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// A single-step work request used in pull mode.
/// One instance is provided each time the work increment advances.
/// </summary>
/// <typeparam name="TIn">Type of the current input value.</typeparam>
/// <param name="Input">The current value to process for this step.</param>
/// <param name="Tags">Optional tags that influence selection for this step.</param>
/// <param name="BranchId">Optional branch identifier to isolate tag state.</param>
/// <param name="CancellationToken">Cancellation token propagated from the orchestrator.</param>
public sealed record GetWorkRequest<TIn>
(
    TIn Input,
    IReadOnlyCollection<string>? Tags = null,
    string? BranchId = null,
    CancellationToken CancellationToken = default
);
