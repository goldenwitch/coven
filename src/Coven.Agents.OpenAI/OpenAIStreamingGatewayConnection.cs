// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIStreamingGatewayConnection(
    OpenAIClientConfig configuration,
    [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> journal,
    ILogger<OpenAIStreamingGatewayConnection> logger,
    OpenAIClient openAIClient,
    IOpenAITranscriptBuilder transcriptBuilder) : IOpenAIGatewayConnection
{
    private readonly OpenAIClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<OpenAIEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly OpenAIResponseClient _client = openAIClient.GetOpenAIResponseClient(configuration.Model) ?? throw new ArgumentNullException(nameof(openAIClient));
    private readonly IOpenAITranscriptBuilder _transcriptBuilder = transcriptBuilder ?? throw new ArgumentNullException(nameof(transcriptBuilder));

    public Task ConnectAsync()
    {
        OpenAILog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(OpenAIEfferent outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<ResponseItem> input = await _transcriptBuilder.BuildAsync(outgoing, _configuration.HistoryClip ?? int.MaxValue, cancellationToken).ConfigureAwait(false);
        OpenAILog.OutboundSendStart(_logger, input.Count);

        ResponseCreationOptions options = new()
        {
            Temperature = _configuration.Temperature,
            TopP = _configuration.TopP,
            MaxOutputTokenCount = _configuration.MaxOutputTokens
        };

        // Map reasoning effort without exposing SDK types to consumers.
        if (_configuration.ReasoningEffort is not null)
        {
            options.ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = _configuration.ReasoningEffort switch
                {
                    ReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
                    ReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
                    ReasoningEffort.High => ResponseReasoningEffortLevel.High,
                    _ => null
                }
            };
        }

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
                        responseId = created.Response.Id ?? responseId;
                        model = created.Response.Model ?? model;
                        createdAt = created.Response.CreatedAt == default ? createdAt : created.Response.CreatedAt;
                        break;


                    case StreamingResponseOutputTextDeltaUpdate textDelta:
                        if (!string.IsNullOrEmpty(textDelta.Delta))
                        {
                            OpenAIAfferentChunk chunk = new(
                                Sender: "openai",
                                Text: textDelta.Delta,
                                ResponseId: responseId,
                                Timestamp: createdAt,
                                Model: model);
                            await _journal.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                        }
                        break;

                    // Surface reasoning updates as OpenAIThought entries when available.
                    case StreamingResponseOutputItemAddedUpdate itemAdded when itemAdded.Item is ReasoningResponseItem reasoningAdded:
                        {
                            string summary = reasoningAdded.GetSummaryText();
                            if (!string.IsNullOrEmpty(summary))
                            {
                                OpenAIThought thought = new(
                                    Sender: "openai",
                                    Text: summary,
                                    ResponseId: responseId,
                                    Timestamp: createdAt,
                                    Model: model);
                                await _journal.WriteAsync(thought, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        break;

                    case StreamingResponseOutputItemDoneUpdate itemDone when itemDone.Item is ReasoningResponseItem reasoningDone:
                        {
                            // Emit a final reasoning summary when completed/incomplete, if any text is present.
                            string summary = reasoningDone.GetSummaryText();
                            if (!string.IsNullOrEmpty(summary))
                            {
                                OpenAIThought thought = new(
                                    Sender: "openai",
                                    Text: summary,
                                    ResponseId: responseId,
                                    Timestamp: createdAt,
                                    Model: model);
                                await _journal.WriteAsync(thought, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        break;

                    case StreamingResponseOutputTextDoneUpdate doneText:
                        _ = doneText;
                        break;

                    case StreamingResponseCompletedUpdate:
                        break;

                    case StreamingResponseErrorUpdate error:
                        throw new InvalidOperationException($"OpenAI streaming error: {error.Code} {error.Message}");

                    case StreamingResponseFailedUpdate failed when failed.Response is not null:
                        throw new InvalidOperationException($"OpenAI streaming failed: {failed.Response.Status}");

                    default:
                        break;
                }
            }
            OpenAILog.OutboundSendSucceeded(_logger);
        }
        catch (OperationCanceledException)
        {
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
}
