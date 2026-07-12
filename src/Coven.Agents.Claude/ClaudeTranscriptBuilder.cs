// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Agents.Claude;

/// <summary>
/// Default transcript builder that converts journal entries to Claude messages.
/// </summary>
internal sealed class ClaudeTranscriptBuilder(
    [FromKeyedServices("Coven.InternalClaudeScrivener")] IScrivener<ClaudeEntry> journal,
    ITransmuter<ClaudeEntry, ClaudeMessage> entryTransmuter) : IClaudeTranscriptBuilder
{
    private readonly IScrivener<ClaudeEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ITransmuter<ClaudeEntry, ClaudeMessage> _entryTransmuter = entryTransmuter ?? throw new ArgumentNullException(nameof(entryTransmuter));

    public async Task<List<ClaudeMessage>> BuildAsync(ClaudeEfferent outgoing, long outgoingPosition, int? historyClip, CancellationToken cancellationToken)
    {
        List<ClaudeMessage> messages = [];
        int maxMessages = historyClip ?? int.MaxValue;

        // Read entries backwards from the journal (most recent first)
        await foreach ((long _, ClaudeEntry entry) in _journal.ReadBackwardAsync(outgoingPosition, cancellationToken).ConfigureAwait(false))
        {
            // Only include efferent (user) and afferent (assistant) messages, skip acks/chunks/drafts
            if (entry is ClaudeEfferent { Text.Length: > 0 }
                or ClaudeAfferent { Text.Length: > 0 }
                or ClaudeToolUse
                or ClaudeToolResult)
            {
                ClaudeMessage message = await _entryTransmuter.Transmute(entry, cancellationToken).ConfigureAwait(false);
                messages.Add(message);
            }

            if (messages.Count >= maxMessages)
            {
                break;
            }
        }

        // Reverse to get chronological order (oldest first)
        messages.Reverse();

        // Add the current outgoing message
        ClaudeMessage outgoingMessage = await _entryTransmuter.Transmute(outgoing, cancellationToken).ConfigureAwait(false);
        messages.Add(outgoingMessage);

        return MergeAdjacentSameRoleMessages(messages);
    }

    /// <summary>
    /// Claude's Messages API requires user/assistant roles to alternate. Journal replay can
    /// produce adjacent same-role messages (e.g. multiple tool_use entries from one response,
    /// or multiple tool_result entries), so adjacent same-role messages are merged into a
    /// single message whose content is the concatenation of their content blocks.
    /// </summary>
    private static List<ClaudeMessage> MergeAdjacentSameRoleMessages(List<ClaudeMessage> messages)
    {
        List<ClaudeMessage> merged = [];
        foreach (ClaudeMessage message in messages)
        {
            if (merged.Count > 0 && string.Equals(merged[^1].Role, message.Role, StringComparison.Ordinal))
            {
                merged[^1] = new ClaudeMessage
                {
                    Role = message.Role,
                    Content = ClaudeMessageContent.FromBlocks(
                        [.. ToBlocks(merged[^1].Content), .. ToBlocks(message.Content)])
                };
                continue;
            }

            merged.Add(message);
        }

        return merged;
    }

    private static List<ClaudeContentBlock> ToBlocks(ClaudeMessageContent content)
        => content switch
        {
            ClaudeMessageContent.Text text => [new ClaudeContentBlock { Type = "text", Text = text.Value }],
            ClaudeMessageContent.Blocks blocks => blocks.Items,
            _ => throw new InvalidOperationException($"Unknown ClaudeMessageContent type: {content.GetType().Name}")
        };
}
