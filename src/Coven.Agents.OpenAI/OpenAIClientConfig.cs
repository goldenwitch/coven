// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.OpenAI;

/// <summary>
/// Minimal configuration for the OpenAI Responses integration.
/// </summary>
public sealed record OpenAIClientConfig
{
    /// <summary>
    /// API key used to authenticate with the OpenAI Responses API.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// The model identifier to use for responses (e.g., <c>gpt-5-2025-08-07</c>).
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Optional organization id for the client.
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Optional project id for the client.
    /// </summary>
    public string? Project { get; init; }

    /// <summary>
    /// Temperature sampling setting (model-dependent).
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Top-p nucleus sampling (model-dependent).
    /// </summary>
    public float? TopP { get; init; }

    /// <summary>
    /// Maximum number of output tokens to generate.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Max number of transcript items to include; defaults to unlimited.
    /// </summary>
    public int HistoryClip { get; init; } = int.MaxValue;

    /// <summary>
    /// Configures reasoning options for models that support it.
    /// </summary>
    public ReasoningConfig Reasoning { get; init; } = new ReasoningConfig();
}
