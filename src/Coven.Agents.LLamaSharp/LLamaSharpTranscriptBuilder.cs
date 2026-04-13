// SPDX-License-Identifier: BUSL-1.1

using System.Text;
using Coven.Core;
using LLama;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Default transcript builder that converts journal entries to a formatted prompt string.
/// Uses the model's embedded GGUF chat template (<see cref="LLamaTemplate"/>) to produce
/// correctly formatted prompts for any supported model architecture.
/// </summary>
internal sealed class LLamaSharpTranscriptBuilder(
    [FromKeyedServices("Coven.InternalLLamaSharpScrivener")] IScrivener<LLamaSharpEntry> journal,
    LLamaSharpClientConfig config) : ILLamaSharpTranscriptBuilder
{
    private readonly IScrivener<LLamaSharpEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly LLamaSharpClientConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    public async Task<string> BuildAsync(LLamaWeights weights, LLamaSharpEfferent outgoing, int? historyClip, CancellationToken cancellationToken)
    {
        List<(string Role, string Text)> messages = [];
        int maxMessages = historyClip ?? int.MaxValue;
        bool skippedCurrentOutgoing = false;

        // Read entries backwards from the journal (most recent first)
        await foreach ((long _, LLamaSharpEntry entry) in _journal.ReadBackwardAsync(long.MaxValue, cancellationToken).ConfigureAwait(false))
        {
            // Only include efferent (user) and afferent (assistant) messages, skip acks/chunks/drafts
            if (entry is LLamaSharpEfferent { Text.Length: > 0 } efferent)
            {
                // The scrivener writes the current outgoing to the journal before SendAsync,
                // so skip the most-recent efferent that matches to avoid duplicating it.
                if (!skippedCurrentOutgoing && efferent.Text == outgoing.Text)
                {
                    skippedCurrentOutgoing = true;
                    continue;
                }

                messages.Add(("user", efferent.Text));
            }
            else if (entry is LLamaSharpAfferent { Text.Length: > 0 } afferent)
            {
                messages.Add(("assistant", afferent.Text));
            }

            if (messages.Count >= maxMessages)
            {
                break;
            }
        }

        // Reverse to get chronological order (oldest first)
        messages.Reverse();

        // Build prompt using the model's native chat template
        LLamaTemplate template = new(weights, strict: false)
        {
            AddAssistant = true
        };

        if (!string.IsNullOrWhiteSpace(_config.SystemPrompt))
        {
            template.Add("system", _config.SystemPrompt);
        }

        foreach ((string role, string text) in messages)
        {
            template.Add(role, text);
        }

        // Add the current outgoing message
        template.Add("user", outgoing.Text);

        return Encoding.UTF8.GetString(template.Apply());
    }
}
