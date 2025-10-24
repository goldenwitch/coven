// SPDX-License-Identifier: BUSL-1.1

using System.ClientModel;
using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;
using Coven.Transmutation;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIGatewayConnection(
    OpenAIClientConfig configuration,
    [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> journal,
    ILogger<OpenAIGatewayConnection> logger,
    OpenAIClient openAIClient,
    ITransmuter<OpenAIEntry, ResponseItem?> entryToItem)
{
    private readonly OpenAIClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<OpenAIEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly OpenAIResponseClient _client = openAIClient.GetOpenAIResponseClient(configuration.Model) ?? throw new ArgumentNullException(nameof(openAIClient));
    private readonly ITransmuter<OpenAIEntry, ResponseItem?> _entryToItem = entryToItem ?? throw new ArgumentNullException(nameof(entryToItem));

    public Task ConnectAsync()
    {
        OpenAILog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(OpenAIOutgoing outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Build request using official OpenAI .NET SDK
        // Include limited history of prior assistant responses from the journal
        List<ResponseItem> input = await BuildTranscriptInputAsync(outgoing, _configuration.HistoryClip ?? int.MaxValue, cancellationToken).ConfigureAwait(false);
        OpenAILog.OutboundSendStart(_logger, input.Count);

        ResponseCreationOptions options = new()
        {
            Temperature = _configuration.Temperature,
            TopP = _configuration.TopP,
            MaxOutputTokenCount = _configuration.MaxOutputTokens
        };

        OpenAIResponse response;
        try
        {
            ClientResult<OpenAIResponse> result = await _client.CreateResponseAsync(input, options, cancellationToken).ConfigureAwait(false);
            response = result.Value;
            OpenAILog.OutboundSendSucceeded(_logger);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw;
        }

        // Extract assistant text; aggregate if necessary
        string text = response.GetOutputText() ?? string.Empty;

        OpenAIIncoming incoming = new(
            Sender: "openai",
            Text: text,
            ResponseId: response.Id,
            Timestamp: response.CreatedAt,
            Model: response.Model);

        await _journal.WriteAsync(incoming, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<ResponseItem>> BuildTranscriptInputAsync(OpenAIOutgoing newest, int maxMessages, CancellationToken cancellationToken)
    {
        List<ResponseItem> buffer = [];

        // Walk the journal backward and collect user/assistant text entries via transmuter.
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

        buffer.Reverse(); // chronological

        // Add the newest outgoing as the final user message
        ResponseItem? newestItem = await _entryToItem.Transmute(newest, cancellationToken).ConfigureAwait(false);
        if (newestItem is not null)
        {
            buffer.Add(newestItem);
        }
        return buffer;
    }
}
