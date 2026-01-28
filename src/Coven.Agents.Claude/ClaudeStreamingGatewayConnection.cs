// SPDX-License-Identifier: BUSL-1.1

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Claude;

internal sealed class ClaudeStreamingGatewayConnection(
    ClaudeClientConfig configuration,
    [FromKeyedServices("Coven.InternalClaudeScrivener")] IScrivener<ClaudeEntry> journal,
    ILogger<ClaudeStreamingGatewayConnection> logger,
    IClaudeTranscriptBuilder transcriptBuilder,
    ITransmuter<ClaudeClientConfig, ClaudeRequestOptions> responseOptionsTransmuter) : IClaudeGatewayConnection, IAsyncDisposable
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ClaudeClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<ClaudeEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IClaudeTranscriptBuilder _transcriptBuilder = transcriptBuilder ?? throw new ArgumentNullException(nameof(transcriptBuilder));
    private readonly ITransmuter<ClaudeClientConfig, ClaudeRequestOptions> _responseOptionsTransmuter = responseOptionsTransmuter ?? throw new ArgumentNullException(nameof(responseOptionsTransmuter));
    private readonly HttpClient _httpClient = CreateHttpClient(configuration);

    public Task ConnectAsync()
    {
        ClaudeLog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(ClaudeEfferent outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<ClaudeMessage> messages = await _transcriptBuilder.BuildAsync(outgoing, _configuration.HistoryClip, cancellationToken).ConfigureAwait(false);
        ClaudeLog.OutboundSendStart(_logger, messages.Count);

        ClaudeRequestOptions options = await _responseOptionsTransmuter.Transmute(_configuration, cancellationToken).ConfigureAwait(false);

        ClaudeMessagesRequest request = new()
        {
            Model = _configuration.Model,
            Messages = messages,
            MaxTokens = options.MaxTokens ?? 4096,
            System = options.System,
            Temperature = options.Temperature,
            TopP = options.TopP,
            TopK = options.TopK,
            StopSequences = options.StopSequences,
            Stream = true,
            Thinking = options.Thinking
        };

        string path = BuildPath();
        using HttpRequestMessage httpRequest = new(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, _serializerOptions), Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        string messageId = Guid.NewGuid().ToString("N");
        string model = _configuration.Model;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);

        string? currentEventType = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            // SSE format: event lines start with "event:", data lines start with "data:"
            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                currentEventType = line["event:".Length..].Trim();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string data = line["data:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(data) || string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ClaudeLog.StreamLine(_logger, data);

            ClaudeStreamEvent? streamEvent;
            try
            {
                streamEvent = JsonSerializer.Deserialize<ClaudeStreamEvent>(data, _serializerOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (streamEvent is null)
            {
                continue;
            }

            // Handle different event types
            switch (currentEventType ?? streamEvent.Type)
            {
                case "message_start":
                    if (streamEvent.Message is not null)
                    {
                        messageId = streamEvent.Message.Id ?? messageId;
                        model = streamEvent.Message.Model ?? model;
                    }
                    break;

                case "content_block_start":
                    // Content block starting - could be text or thinking
                    break;

                case "content_block_delta":
                    await HandleContentDelta(streamEvent, messageId, timestamp, model, cancellationToken).ConfigureAwait(false);
                    break;

                case "content_block_stop":
                    // Content block completed
                    break;

                case "message_delta":
                    // Message-level updates (stop_reason, usage, etc.)
                    break;

                case "message_stop":
                    // Message complete
                    break;

                case "error":
                    if (streamEvent.Error is not null)
                    {
                        throw new InvalidOperationException($"Claude streaming error: {streamEvent.Error.Type} - {streamEvent.Error.Message}");
                    }
                    break;

                case "ping":
                    // Heartbeat event, ignore
                    break;

                default:
                    // Unknown event type, ignore
                    break;
            }
        }

        ClaudeStreamCompleted done = new(
            Sender: "claude",
            MessageId: messageId,
            Timestamp: timestamp,
            Model: model);
        await _journal.WriteAsync(done, cancellationToken).ConfigureAwait(false);

        ClaudeLog.OutboundSendSucceeded(_logger);
    }

    private async Task HandleContentDelta(ClaudeStreamEvent streamEvent, string messageId, DateTimeOffset timestamp, string model, CancellationToken cancellationToken)
    {
        if (streamEvent.Delta is null)
        {
            return;
        }

        // Handle text delta
        if (string.Equals(streamEvent.Delta.Type, "text_delta", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(streamEvent.Delta.Text))
        {
            ClaudeAfferentChunk chunk = new(
                Sender: "claude",
                Text: streamEvent.Delta.Text,
                MessageId: messageId,
                Timestamp: timestamp,
                Model: model);
            await _journal.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
        }

        // Handle thinking delta (extended thinking)
        if (string.Equals(streamEvent.Delta.Type, "thinking_delta", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(streamEvent.Delta.Thinking))
        {
            ClaudeAfferentThinkingChunk thinkingChunk = new(
                Sender: "claude",
                Text: streamEvent.Delta.Thinking,
                MessageId: messageId,
                Timestamp: timestamp,
                Model: model);
            await _journal.WriteAsync(thinkingChunk, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private string BuildPath()
    {
        string baseUrl = _configuration.Endpoint?.ToString()?.TrimEnd('/') ?? "https://api.anthropic.com";
        return $"{baseUrl}/v1/messages";
    }

    private static HttpClient CreateHttpClient(ClaudeClientConfig configuration)
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.Add("x-api-key", configuration.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Claude API error {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                null,
                response.StatusCode);
        }
    }
}
