// SPDX-License-Identifier: BUSL-1.1

using System.ClientModel;
using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIGatewayConnection(
    OpenAIClientConfig configuration,
    [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> journal,
    ILogger<OpenAIGatewayConnection> logger,
    OpenAIClient openAIClient)
{
    private readonly OpenAIClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<OpenAIEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly OpenAIClient _client = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));

    public Task ConnectAsync()
    {
        OpenAILog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(OpenAIOutgoing outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Build request using official OpenAI .NET SDK
        OpenAILog.OutboundSendStart(_logger, outgoing.Text.Length);

        // Create a per-model Responses client and prepare options
        OpenAIResponseClient responsesClient = _client.GetOpenAIResponseClient(_configuration.Model);

        ResponseCreationOptions options = new()
        {
            Temperature = _configuration.Temperature,
            TopP = _configuration.TopP,
            MaxOutputTokenCount = _configuration.MaxOutputTokens
        };

        OpenAIResponse response;
        try
        {
            ClientResult<OpenAIResponse> result = await responsesClient.CreateResponseAsync(outgoing.Text, options, cancellationToken).ConfigureAwait(false);
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
}
