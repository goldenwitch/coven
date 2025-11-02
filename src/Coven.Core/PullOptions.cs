// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// Options to tune Pull-mode ritual completion behavior.
/// </summary>
public sealed class PullOptions
{
    /// <summary>
    /// Optional predicate deciding whether the ritual should complete when an assignable value is observed.
    /// Returning <c>true</c> completes with that value; returning <c>false</c> continues stepping.
    /// When <c>null</c>, completion occurs immediately upon first assignable value.
    /// </summary>
    public Func<object, bool>? ShouldComplete { get; init; }

    /// <summary>
    /// Back-compat: only applied to the very first input (pre-step). Prefer <see cref="ShouldComplete"/>.
    /// </summary>
    public Func<object, bool>? IsInitialComplete { get; init; }
}
