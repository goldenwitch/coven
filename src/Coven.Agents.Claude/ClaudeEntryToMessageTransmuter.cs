// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;
using Coven.Transmutation;

namespace Coven.Agents.Claude;

/// <summary>
/// Converts Claude journal entries to Claude API message format.
/// </summary>
internal sealed class ClaudeEntryToMessageTransmuter : ITransmuter<ClaudeEntry, ClaudeMessage>
{
    public Task<ClaudeMessage> Transmute(ClaudeEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ClaudeEfferent efferent => Task.FromResult(new ClaudeMessage
            {
                Role = "user",
                Content = ClaudeMessageContent.FromText(efferent.Text)
            }),
            ClaudeAfferent afferent => Task.FromResult(new ClaudeMessage
            {
                Role = "assistant",
                Content = ClaudeMessageContent.FromText(afferent.Text)
            }),
            ClaudeToolUse toolUse => Task.FromResult(new ClaudeMessage
            {
                Role = "assistant",
                Content = ClaudeMessageContent.FromBlocks(
                [
                    new ClaudeContentBlock
                    {
                        Type = "tool_use",
                        Id = toolUse.ToolUseId,
                        Name = toolUse.ToolName,
                        Input = JsonDocument.Parse(toolUse.ArgumentsJson).RootElement.Clone()
                    }
                ])
            }),
            ClaudeToolResult toolResult => Task.FromResult(new ClaudeMessage
            {
                Role = "user",
                Content = ClaudeMessageContent.FromBlocks(
                [
                    new ClaudeContentBlock
                    {
                        Type = "tool_result",
                        ToolUseId = toolResult.ToolUseId,
                        Content = toolResult.Result,
                        IsError = toolResult.IsError ? true : null
                    }
                ])
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(Input), $"Cannot convert {Input.GetType().Name} to ClaudeMessage")
        };
    }
}
