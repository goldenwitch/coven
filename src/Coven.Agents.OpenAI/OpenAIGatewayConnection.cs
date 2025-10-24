// SPDX-License-Identifier: BUSL-1.1

using System.ClientModel;
using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;
using Coven.Transmutation;
using Coven.Agents.Streaming;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIGatewayConnection(
    OpenAIClientConfig configuration,
    [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> journal,
    ILogger<OpenAIGatewayConnection> logger,
    OpenAIClient openAIClient,
    ITransmuter<OpenAIEntry, ResponseItem?> entryToItem,
    AgentStreamingOptions streamingOptions)
{
    private readonly OpenAIClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<OpenAIEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly OpenAIResponseClient _client = openAIClient.GetOpenAIResponseClient(configuration.Model) ?? throw new ArgumentNullException(nameof(openAIClient));
    private readonly ITransmuter<OpenAIEntry, ResponseItem?> _entryToItem = entryToItem ?? throw new ArgumentNullException(nameof(entryToItem));
    private readonly AgentStreamingOptions _streaming = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));

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

        if (_streaming.Enabled)
        {
            string model = _configuration.Model;
            string responseId = string.Empty;
            DateTimeOffset createdAt = DateTimeOffset.UtcNow;

            try
            {
                await foreach (StreamingResponseUpdate update in _client.CreateResponseStreamingAsync(input, options, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    switch (update)
                    {
                        case StreamingResponseCreatedUpdate created when created.Response is not null:
                            // Initialize identifiers from the first created event
                            responseId = created.Response.Id ?? responseId;
                            model = created.Response.Model ?? model;
                            createdAt = created.Response.CreatedAt == default ? createdAt : created.Response.CreatedAt;
                            break;

                        case StreamingResponseOutputTextDeltaUpdate textDelta:
                            if (!string.IsNullOrEmpty(textDelta.Delta))
                            {
                                OpenAIIncomingChunk chunk = new(
                                    Sender: "openai",
                                    Text: textDelta.Delta,
                                    ResponseId: responseId,
                                    Timestamp: createdAt,
                                    Model: model);
                                await _journal.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                            }
                            break;

                        case StreamingResponseOutputTextDoneUpdate doneText:
                            // Acknowledge completion of a text span; no-op here (segmentation flushes on final event below)
                            _ = doneText;
                            break;

                        case StreamingResponseCompletedUpdate:
                            // Final completion signal; loop will end after this item
                            break;

                        case StreamingResponseErrorUpdate error:
                            // Surface as exception with available info
                            throw new InvalidOperationException($"OpenAI streaming error: {error.Code} {error.Message}");

                        case StreamingResponseFailedUpdate failed when failed.Response is not null:
                            throw new InvalidOperationException($"OpenAI streaming failed: {failed.Response.Status}");

                        default:
                            // Ignore other update types (tools, images, file/web search, etc.) for now
                            break;
                    }
                }
                OpenAILog.OutboundSendSucceeded(_logger);
            }
            catch (OperationCanceledException)
            {
                // Cooperative cancellation
            }
            catch (Exception)
            {
                throw;
            }

            OpenAIStreamCompleted done = new(
                Sender: "openai",
                ResponseId: responseId,
                Timestamp: createdAt,
                Model: model);
            await _journal.WriteAsync(done, cancellationToken).ConfigureAwait(false);
        }
        else
        {
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

            string text = response.GetOutputText() ?? string.Empty;

            OpenAIIncoming incoming = new(
                Sender: "openai",
                Text: text,
                ResponseId: response.Id,
                Timestamp: response.CreatedAt,
                Model: response.Model);

            await _journal.WriteAsync(incoming, cancellationToken).ConfigureAwait(false);
        }
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
