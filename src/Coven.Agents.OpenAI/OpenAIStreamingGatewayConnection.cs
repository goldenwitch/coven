// SPDX-License-Identifier: BUSL-1.1

using System.Reflection;
using Coven.Core.Scrivener;
using Coven.Transmutation;
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
    IOpenAITranscriptBuilder transcriptBuilder,
    ITransmuter<OpenAIClientConfig, ResponseCreationOptions> responseOptionsTransmuter) : IOpenAIGatewayConnection
{
    private readonly OpenAIClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<OpenAIEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly OpenAIResponseClient _client = openAIClient.GetOpenAIResponseClient(configuration.Model) ?? throw new ArgumentNullException(nameof(openAIClient));
    private readonly IOpenAITranscriptBuilder _transcriptBuilder = transcriptBuilder ?? throw new ArgumentNullException(nameof(transcriptBuilder));
    private readonly ITransmuter<OpenAIClientConfig, ResponseCreationOptions> _responseOptionsTransmuter = responseOptionsTransmuter ?? throw new ArgumentNullException(nameof(responseOptionsTransmuter));

    public Task ConnectAsync()
    {
        OpenAILog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(OpenAIEfferent outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<ResponseItem> input = await _transcriptBuilder.BuildAsync(outgoing, _configuration.HistoryClip, cancellationToken).ConfigureAwait(false);
        OpenAILog.OutboundSendStart(_logger, input.Count);

        ResponseCreationOptions options = await _responseOptionsTransmuter.Transmute(_configuration, cancellationToken).ConfigureAwait(false);

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

                    case StreamingResponseOutputTextDoneUpdate doneText:
                        _ = doneText;
                        break;

                    case StreamingResponseCompletedUpdate:
                        break;

                    case StreamingResponseErrorUpdate error:
                        throw new InvalidOperationException($"OpenAI streaming error: {error.Code} {error.Message}");

                    case StreamingResponseFailedUpdate failed when failed.Response is not null:
                        throw new InvalidOperationException($"OpenAI streaming failed: {failed.Response.Status}");

                    case var rsDelta when TryGetReasoningSummaryTextDelta(update, out string? rDelta) && !string.IsNullOrEmpty(rDelta):
                        {
                            OpenAIAfferentThoughtChunk tChunk = new(
                                Sender: "openai",
                                Text: rDelta,
                                ResponseId: responseId,
                                Timestamp: createdAt,
                                Model: model);
                            await _journal.WriteAsync(tChunk, cancellationToken).ConfigureAwait(false);
                            break;
                        }

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

    /// <summary>
    /// Filthy shim. Do not copy this, this is bad code. We just have this here to handle people being too slow to update the official SDK.
    /// </summary>
    /// <param name="update"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    private static bool TryGetReasoningSummaryTextDelta(object? update, out string? delta)
    {
        delta = null;
        if (update is null)
        {
            return false;
        }

        Type t = update.GetType();
        // Current SDK keeps these types internal; detect by simple name to avoid tight coupling.
        if (t.Name is "StreamingResponseReasoningSummaryTextDeltaUpdate" or "InternalResponseReasoningSummaryTextDeltaEvent")
        {
            // internal/public both expose a string Delta; grab it reflexively
            PropertyInfo? prop = t.GetProperty("Delta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            delta = prop?.GetValue(update)?.ToString();
            return !string.IsNullOrEmpty(delta);
        }
        return false;
    }
}
