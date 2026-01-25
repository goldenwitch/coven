// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Gemini.Client;

/// <summary>
/// Configuration options for <see cref="GeminiRestClient"/>.
/// </summary>
public sealed record GeminiRestClientOptions
{
    /// <summary>API key used to authenticate with the Gemini API.</summary>
    public required string ApiKey { get; init; }

    /// <summary>The model identifier (e.g., gemini-3.0-pro or gemini-2.0-flash).</summary>
    public required string Model { get; init; }

    /// <summary>Optional override for the Gemini API endpoint; defaults to generativelanguage.googleapis.com.</summary>
    public Uri? Endpoint { get; init; }

    /// <summary>Request timeout; defaults to 5 minutes.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
}
