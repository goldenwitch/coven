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

    public async Task<List<ClaudeMessage>> BuildAsync(ClaudeEfferent outgoing, int? historyClip, CancellationToken cancellationToken)
    {
        List<ClaudeMessage> messages = [];
        int maxMessages = historyClip ?? 100; // Default to 100 messages if no clip specified

        // Read entries backwards from the journal (most recent first)
        await foreach ((long _, ClaudeEntry entry) in _journal.ReadBackwardAsync(long.MaxValue, cancellationToken).ConfigureAwait(false))
        {
            // Only include efferent (user) and afferent (assistant) messages, skip acks/chunks/drafts
            if (entry is ClaudeEfferent { Text.Length: > 0 } or ClaudeAfferent { Text.Length: > 0 })
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

        return messages;
    }
}
