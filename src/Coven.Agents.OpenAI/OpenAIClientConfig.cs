// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Minimal configuration for the OpenAI Responses integration.
/// </summary>
public sealed class OpenAIClientConfig
{
    public required string ApiKey { get; init; }
    public required string Model { get; init; }

    public string? Organization { get; init; }
    public string? Project { get; init; }

    public float? Temperature { get; init; }
    public float? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
    public int? HistoryClip { get; init; }

    // Configures reasoning effort for models that support it.
    public ReasoningEffort? ReasoningEffort { get; init; }
}
