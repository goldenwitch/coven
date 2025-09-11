// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

// Options to tune Pull mode behavior.
public sealed class PullOptions
{
    // If provided, this delegate decides whether the ritual should complete
    // when a value (initial or step output) is assignable to the requested final type.
    // Returning true completes the ritual with that value; returning false continues stepping.
    // When null, default behavior is to complete immediately when assignable.
    public Func<object, bool>? ShouldComplete { get; init; }

    // Back-compat: if specified, only applies to the very first input before any steps.
    // Prefer ShouldComplete for consistent behavior across all steps.
    public Func<object, bool>? IsInitialComplete { get; init; }
}