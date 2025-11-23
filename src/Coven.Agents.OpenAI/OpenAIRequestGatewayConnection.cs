// SPDX-License-Identifier: BUSL-1.1

using System.ClientModel;
using Coven.Core.Scrivener;
using Coven.Transmutation;
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
