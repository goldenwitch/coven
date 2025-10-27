// SPDX-License-Identifier: BUSL-1.1

using System.ClientModel;
using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIRequestGatewayConnection(
    OpenAIClientConfig configuration,
    [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> journal,
    ILogger<OpenAIRequestGatewayConnection> logger,
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

        // Surface any reasoning/thought summaries if present in the non-streaming response.
        if (response.OutputItems is not null)
        {
            foreach (ResponseItem item in response.OutputItems)
            {
                if (item is ReasoningResponseItem reasoning)
                {
                    string summary = reasoning.GetSummaryText();
                    if (!string.IsNullOrEmpty(summary))
                    {
                        OpenAIThought thought = new(
                            Sender: "openai",
                            Text: summary,
                            ResponseId: response.Id,
                            Timestamp: response.CreatedAt,
                            Model: response.Model);
                        await _journal.WriteAsync(thought, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        string text = response.GetOutputText() ?? string.Empty;

        OpenAIAfferent incoming = new(
            Sender: "openai",
            Text: text,
            ResponseId: response.Id,
            Timestamp: response.CreatedAt,
            Model: response.Model);
        await _journal.WriteAsync(incoming, cancellationToken).ConfigureAwait(false);
    }
}
