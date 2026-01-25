// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Gemini.Client;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Agents.Gemini;

internal sealed class GeminiTranscriptBuilder(
    [FromKeyedServices("Coven.InternalGeminiScrivener")] IScrivener<GeminiEntry> journal,
    ITransmuter<GeminiEntry, GeminiContent?> entryToContent)
    : IGeminiTranscriptBuilder
{
    private readonly IScrivener<GeminiEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ITransmuter<GeminiEntry, GeminiContent?> _entryToContent = entryToContent ?? throw new ArgumentNullException(nameof(entryToContent));

    public async Task<List<GeminiContent>> BuildAsync(GeminiEfferent newest, int maxMessages, CancellationToken cancellationToken)
    {
        List<GeminiContent> buffer = [];

        await foreach ((_, GeminiEntry entry) in _journal.ReadBackwardAsync(long.MaxValue, cancellationToken).ConfigureAwait(false))
        {
            GeminiContent? content = await _entryToContent.Transmute(entry, cancellationToken).ConfigureAwait(false);
            if (content is not null)
            {
                buffer.Add(content);
            }

            if (buffer.Count >= maxMessages)
            {
                break;
            }
        }

        buffer.Reverse();

        GeminiContent? newestContent = await _entryToContent.Transmute(newest, cancellationToken).ConfigureAwait(false);
        if (newestContent is not null)
        {
            buffer.Add(newestContent);
        }
        return buffer;
    }
}
