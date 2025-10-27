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

        OpenAIAfferent incoming = new(
            Sender: "openai",
            Text: text,
            ResponseId: response.Id,
            Timestamp: response.CreatedAt,
            Model: response.Model);

        await _journal.WriteAsync(incoming, cancellationToken).ConfigureAwait(false);
    }
}
