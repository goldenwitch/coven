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
    private readonly OpenAIResponseClient _client = openAIClient.GetOpenAIResponseClient(configuration.Model) ?? throw new ArgumentNullException(nameof(openAIClient));

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

        // Walk the journal backward and collect user/assistant text entries.
        await foreach ((_, OpenAIEntry entry) in _journal.ReadBackwardAsync(long.MaxValue, cancellationToken).ConfigureAwait(false))
        {
            switch (entry)
            {
                case OpenAIOutgoing u when !string.IsNullOrWhiteSpace(u.Text):
                    buffer.Add(ResponseItem.CreateUserMessageItem(u.Text));
                    break;

                case OpenAIIncoming a when !string.IsNullOrWhiteSpace(a.Text):
                    buffer.Add(ResponseItem.CreateAssistantMessageItem(a.Text));
                    break;

                default:
                    // ignore other entry types (tool traces, etc.) in this minimal window
                    break;
            }

            if (buffer.Count >= maxMessages)
            {
                break;
            }
        }

        buffer.Reverse(); // chronological

        buffer.Add(ResponseItem.CreateUserMessageItem(newest.Text));
        return buffer;
    }
}
