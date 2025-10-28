// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Minimal configuration for the OpenAI Responses integration.
/// </summary>
public sealed record OpenAIClientConfig
{
    public required string ApiKey { get; init; }
    public required string Model { get; init; }

    public string? Organization { get; init; }
    public string? Project { get; init; }

    public float? Temperature { get; init; }
    public float? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
    // Max number of transcript items to include; default is unlimited
    public int HistoryClip { get; init; } = int.MaxValue;

    // Configures reasoning options for models that support it.
    public ReasoningConfig Reasoning { get; init; } = new ReasoningConfig();
}
