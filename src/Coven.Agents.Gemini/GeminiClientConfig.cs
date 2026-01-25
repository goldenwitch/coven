// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Gemini;

/// <summary>
/// Minimal configuration for the Gemini integration.
/// </summary>
public sealed record GeminiClientConfig
{
    /// <summary>API key used to authenticate with the Gemini API.</summary>
    public required string ApiKey { get; init; }

    /// <summary>The model identifier (e.g., gemini-3.0-pro or gemini-2.0-flash).</summary>
    public required string Model { get; init; }

    /// <summary>Temperature sampling setting.</summary>
    public float? Temperature { get; init; }

    /// <summary>Top-p nucleus sampling.</summary>
    public float? TopP { get; init; }

    /// <summary>Top-k sampling.</summary>
    public int? TopK { get; init; }

    /// <summary>Maximum number of output tokens to generate.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Optional system instruction text injected into the request.</summary>
    public string? SystemInstruction { get; init; }

    /// <summary>Optional MIME type for structured responses (e.g., application/json).</summary>
    public string? ResponseMimeType { get; init; }

    /// <summary>Maximum number of transcript items to include; defaults to unlimited.</summary>
    public int HistoryClip { get; init; } = int.MaxValue;

    /// <summary>Optional safety settings (category + threshold).</summary>
    public IReadOnlyList<GeminiSafetySetting>? SafetySettings { get; init; }

    /// <summary>Optional override for the Gemini API endpoint; defaults to generativelanguage.googleapis.com.</summary>
    public Uri? Endpoint { get; init; }
}

/// <summary>Represents a Gemini safety setting tuple.</summary>
public sealed record GeminiSafetySetting(string Category, string Threshold);
