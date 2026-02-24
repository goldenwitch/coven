// SPDX-License-Identifier: BUSL-1.1

using System.Text;
using Coven.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Default transcript builder that converts journal entries to a formatted prompt string.
/// Reads the journal backward, collects user/assistant turns, and formats them for local model inference.
/// </summary>
internal sealed class LLamaSharpTranscriptBuilder(
    [FromKeyedServices("Coven.InternalLLamaSharpScrivener")] IScrivener<LLamaSharpEntry> journal,
    LLamaSharpClientConfig config) : ILLamaSharpTranscriptBuilder
{
    private readonly IScrivener<LLamaSharpEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly LLamaSharpClientConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    public async Task<string> BuildAsync(LLamaSharpEfferent outgoing, int? historyClip, CancellationToken cancellationToken)
    {
        List<(string Role, string Text)> messages = [];
        int maxMessages = historyClip ?? 100;

        // Read entries backwards from the journal (most recent first)
        await foreach ((long _, LLamaSharpEntry entry) in _journal.ReadBackwardAsync(long.MaxValue, cancellationToken).ConfigureAwait(false))
        {
            // Only include efferent (user) and afferent (assistant) messages, skip acks/chunks/drafts
            if (entry is LLamaSharpEfferent { Text.Length: > 0 } efferent)
            {
                messages.Add(("User", efferent.Text));
            }
            else if (entry is LLamaSharpAfferent { Text.Length: > 0 } afferent)
            {
                messages.Add(("Assistant", afferent.Text));
            }

            if (messages.Count >= maxMessages)
            {
                break;
            }
        }

        // Reverse to get chronological order (oldest first)
        messages.Reverse();

        // Build the prompt string
        StringBuilder sb = new();

        if (!string.IsNullOrWhiteSpace(_config.SystemPrompt))
        {
            sb.Append("System: ").AppendLine(_config.SystemPrompt);
            sb.AppendLine();
        }

        foreach ((string role, string text) in messages)
        {
            sb.Append(role).Append(": ").AppendLine(text);
        }

        // Add the current outgoing message
        sb.Append("User: ").AppendLine(outgoing.Text);
        sb.Append("Assistant:");

        return sb.ToString();
    }
}
