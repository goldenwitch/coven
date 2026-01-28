// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.Claude;

/// <summary>
/// Converts Claude client configuration to API request options.
/// </summary>
internal sealed class ClaudeResponseOptionsTransmuter : ITransmuter<ClaudeClientConfig, ClaudeRequestOptions>
{
    public Task<ClaudeRequestOptions> Transmute(ClaudeClientConfig Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ClaudeRequestOptions options = new()
        {
            MaxTokens = Input.MaxTokens ?? 4096,
            System = Input.SystemPrompt,
            Temperature = Input.Temperature,
            TopP = Input.TopP,
            TopK = Input.TopK,
            StopSequences = Input.StopSequences
        };

        // Configure extended thinking if enabled
        if (Input.ExtendedThinking?.Enabled == true)
        {
            options.Thinking = new ClaudeThinkingConfig
            {
                Type = "enabled",
                BudgetTokens = Math.Max(1024, Input.ExtendedThinking.BudgetTokens)
            };
        }

        return Task.FromResult(options);
    }
}
