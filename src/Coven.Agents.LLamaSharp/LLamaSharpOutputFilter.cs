// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Extracts the user-facing response from raw model output that may contain
/// thinking/analysis content produced by reasoning models.
/// </summary>
internal static class LLamaSharpOutputFilter
{
    /// <summary>
    /// If <paramref name="marker"/> is non-null, returns only the text after
    /// the last occurrence of that marker (trimmed). Otherwise returns the
    /// original text unchanged.
    /// </summary>
    internal static string ExtractResponse(string text, string? marker)
    {
        if (marker is null || marker.Length == 0)
        {
            return text;
        }

        int index = text.LastIndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? text : text[(index + marker.Length)..].TrimStart();
    }
}
