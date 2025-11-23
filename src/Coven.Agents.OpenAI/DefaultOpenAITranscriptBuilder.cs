// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;
using Coven.Transmutation;
using Coven.Core.Scrivener;

namespace Coven.Agents.OpenAI;

internal sealed class DefaultOpenAITranscriptBuilder(
    [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> journal,
    ITransmuter<OpenAIEntry, ResponseItem?> entryToItem) : IOpenAITranscriptBuilder
{
    private readonly IScrivener<OpenAIEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ITransmuter<OpenAIEntry, ResponseItem?> _entryToItem = entryToItem ?? throw new ArgumentNullException(nameof(entryToItem));

    public async Task<List<ResponseItem>> BuildAsync(OpenAIEfferent newest, int maxMessages, CancellationToken cancellationToken)
    {
        List<ResponseItem> buffer = [];

        await foreach ((_, OpenAIEntry entry) in _journal.ReadBackwardAsync(long.MaxValue, cancellationToken).ConfigureAwait(false))
        {
            ResponseItem? item = await _entryToItem.Transmute(entry, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                buffer.Add(item);
            }

            if (buffer.Count >= maxMessages)
            {
                break;
            }
        }

        buffer.Reverse();

        ResponseItem? newestItem = await _entryToItem.Transmute(newest, cancellationToken).ConfigureAwait(false);
        if (newestItem is not null)
        {
            buffer.Add(newestItem);
        }
        return buffer;
    }
}

