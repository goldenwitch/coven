// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Claude;

/// <summary>
/// Configuration for the Claude API client.
/// </summary>
public sealed class ClaudeClientConfig
{
    /// <summary>Gets or sets the Anthropic API key (required).</summary>
    public required string ApiKey { get; set; }

    /// <summary>Gets or sets the model identifier (required), e.g., "claude-sonnet-4-20250514".</summary>
    public required string Model { get; set; }

    /// <summary>Gets or sets the optional base endpoint URL. Defaults to Anthropic's API.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>Gets or sets the maximum number of tokens to generate.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Gets or sets the sampling temperature (0.0 to 1.0).</summary>
    public float? Temperature { get; set; }

    /// <summary>Gets or sets the top-p (nucleus) sampling parameter.</summary>
    public float? TopP { get; set; }

    /// <summary>Gets or sets the top-k sampling parameter.</summary>
    public int? TopK { get; set; }

    /// <summary>Gets or sets the system prompt to prepend to conversations.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Gets or sets optional stop sequences.</summary>
    public IReadOnlyList<string>? StopSequences { get; set; }

    /// <summary>Gets or sets the maximum number of transcript items to include in requests (default unlimited).</summary>
    public int? HistoryClip { get; set; }

    /// <summary>Gets or sets the extended thinking configuration.</summary>
    public ExtendedThinkingConfig? ExtendedThinking { get; set; }
}

/// <summary>
/// Configuration for Claude's extended thinking feature.
/// </summary>
public sealed class ExtendedThinkingConfig
{
    /// <summary>Gets or sets whether extended thinking is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the budget tokens for thinking (minimum 1024).</summary>
    public int BudgetTokens { get; set; } = 10000;
}
