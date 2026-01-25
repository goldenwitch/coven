// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Gemini.Client;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Agents.Gemini;

internal sealed class GeminiTranscriptBuilder(
    [FromKeyedServices("Coven.InternalGeminiScrivener")] IScrivener<GeminiEntry> journal,
    ITransmuter<GeminiEntry, GeminiContent> entryToContent)
    : IGeminiTranscriptBuilder
{
    private readonly IScrivener<GeminiEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ITransmuter<GeminiEntry, GeminiContent> _entryToContent = entryToContent ?? throw new ArgumentNullException(nameof(entryToContent));

    public async Task<List<GeminiContent>> BuildAsync(GeminiEfferent newest, int maxMessages, CancellationToken cancellationToken)
    {
        List<GeminiContent> buffer = [];

        await foreach ((_, GeminiEntry entry) in _journal.ReadBackwardAsync(long.MaxValue, cancellationToken).ConfigureAwait(false))
        {
            // Filter: only user/assistant text entries participate in transcripts
            if (entry is GeminiEfferent { Text.Length: > 0 } or GeminiAfferent { Text.Length: > 0 })
            {
                GeminiContent content = await _entryToContent.Transmute(entry, cancellationToken).ConfigureAwait(false);
                buffer.Add(content);
            }

            if (buffer.Count >= maxMessages)
            {
                break;
            }
        }

        buffer.Reverse();

        // Newest entry is always GeminiEfferent with text
        if (!string.IsNullOrEmpty(newest.Text))
        {
            GeminiContent newestContent = await _entryToContent.Transmute(newest, cancellationToken).ConfigureAwait(false);
            buffer.Add(newestContent);
        }
        return buffer;
    }
}
