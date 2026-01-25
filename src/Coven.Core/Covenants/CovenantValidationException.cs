// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Thrown when covenant validation fails at build time.
/// Contains detailed information about which routes are missing or misconfigured.
/// </summary>
/// <param name="errors">List of validation error messages.</param>
public sealed class CovenantValidationException(IReadOnlyList<string> errors)
    : Exception(FormatMessage(errors))
{
    /// <summary>
    /// Individual validation errors that caused the failure.
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = errors;

    private static string FormatMessage(IReadOnlyList<string> errors)
    {
        return $"Covenant validation failed with {errors.Count} error(s):\n" +
               string.Join("\n", errors.Select(e => $"  ✗ {e}"));
    }
}

