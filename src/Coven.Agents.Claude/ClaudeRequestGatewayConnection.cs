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

internal sealed class ClaudeRequestGatewayConnection(
    ClaudeClientConfig configuration,
    [FromKeyedServices("Coven.InternalClaudeScrivener")] IScrivener<ClaudeEntry> journal,
    ILogger<ClaudeRequestGatewayConnection> logger,
    IClaudeTranscriptBuilder transcriptBuilder,
    ITransmuter<ClaudeClientConfig, ClaudeRequestOptions> responseOptionsTransmuter,
    IEnumerable<ToolDefinition> tools) : IClaudeGatewayConnection, IAsyncDisposable
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
    private readonly List<ClaudeToolDefinition> _tools = BuildToolDefinitions(tools);
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

        // Tool call loop: send, handle tool_use, wait for results, repeat
        const int maxToolRoundTrips = 25;
        int toolRoundTrips = 0;
        while (true)
        {
            ClaudeMessagesResponse messagesResponse = await SendRequestAsync(messages, options, cancellationToken).ConfigureAwait(false);

            string messageId = messagesResponse.Id ?? Guid.NewGuid().ToString("N");
            string model = messagesResponse.Model ?? _configuration.Model;
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;

            // Extract thinking content if present
            string? thinkingText = ExtractThinkingText(messagesResponse);
            if (!string.IsNullOrEmpty(thinkingText))
            {
                ClaudeThought thought = new(
                    Sender: "claude",
                    Text: thinkingText,
                    MessageId: messageId,
                    Timestamp: timestamp,
                    Model: model);
                await _journal.WriteAsync(thought, cancellationToken).ConfigureAwait(false);
            }

            // Check for tool use
            List<ClaudeContentBlock> toolUseBlocks = ExtractToolUseBlocks(messagesResponse);
            if (toolUseBlocks.Count > 0)
            {
                // Write tool use entries to journal (session pump will route via covenant)
                long? firstToolUsePos = null;
                List<string> pendingToolUseIds = [];
                foreach (ClaudeContentBlock block in toolUseBlocks)
                {
                    string toolUseId = block.Id ?? Guid.NewGuid().ToString("N");
                    string argumentsJson = block.Input.HasValue
                        ? block.Input.Value.GetRawText()
                        : "{}";
                    ClaudeToolUse toolUseEntry = new(
                        Sender: "claude",
                        ToolUseId: toolUseId,
                        ToolName: block.Name ?? "unknown",
                        ArgumentsJson: argumentsJson,
                        MessageId: messageId,
                        Timestamp: timestamp,
                        Model: model);
                    long pos = await _journal.WriteAsync(toolUseEntry, cancellationToken).ConfigureAwait(false);
                    firstToolUsePos ??= pos;
                    pendingToolUseIds.Add(toolUseId);
                }

                // Append assistant message with tool_use content to conversation
                List<ClaudeContentBlock> assistantContent = [.. messagesResponse.Content!
                    .Where(b => !string.Equals(b.Type, "thinking", StringComparison.OrdinalIgnoreCase))];
                messages.Add(new ClaudeMessage { Role = "assistant", Content = ClaudeMessageContent.FromBlocks(assistantContent) });

                // Wait for all tool results (routed back via covenant → session pump)
                List<ClaudeContentBlock> toolResultBlocks = [];
                foreach (string toolUseId in pendingToolUseIds)
                {
                    (_, ClaudeToolResult result) = await _journal.WaitForAsync<ClaudeToolResult>(
                        firstToolUsePos!.Value - 1,
                        r => r.ToolUseId == toolUseId,
                        cancellationToken).ConfigureAwait(false);
                    toolResultBlocks.Add(new ClaudeContentBlock
                    {
                        Type = "tool_result",
                        ToolUseId = result.ToolUseId,
                        Content = result.Result,
                        IsError = result.IsError ? true : null
                    });
                }

                // Append tool results as user message
                messages.Add(new ClaudeMessage { Role = "user", Content = ClaudeMessageContent.FromBlocks(toolResultBlocks) });

                // Continue loop — send again with tool results
                if (++toolRoundTrips >= maxToolRoundTrips)
                {
                    throw new InvalidOperationException(
                        $"Claude tool call loop exceeded {maxToolRoundTrips} round-trips. Terminating to prevent infinite loop.");
                }
                continue;
            }

            // No tool calls — write final response
            string textContent = ExtractTextContent(messagesResponse);
            ClaudeAfferent afferent = new(
                Sender: "claude",
                Text: textContent,
                MessageId: messageId,
                Timestamp: timestamp,
                Model: model);
            await _journal.WriteAsync(afferent, cancellationToken).ConfigureAwait(false);

            ClaudeLog.OutboundSendSucceeded(_logger);
            return;
        }
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task<ClaudeMessagesResponse> SendRequestAsync(
        List<ClaudeMessage> messages,
        ClaudeRequestOptions options,
        CancellationToken cancellationToken)
    {
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
            Thinking = options.Thinking,
            Tools = _tools.Count > 0 ? _tools : null
        };

        string path = BuildPath();
        using HttpRequestMessage httpRequest = new(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, _serializerOptions), Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ClaudeMessagesResponse>(responseBody, _serializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Claude response.");
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
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.Add("x-api-key", configuration.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static List<ClaudeToolDefinition> BuildToolDefinitions(IEnumerable<ToolDefinition> tools)
    {
        List<ClaudeToolDefinition> definitions = [];
        foreach (ToolDefinition tool in tools)
        {
            JsonElement? inputSchema = null;
            if (tool.InputSchema is not null)
            {
                using JsonDocument doc = JsonDocument.Parse(tool.InputSchema);
                inputSchema = doc.RootElement.Clone();
            }
            definitions.Add(new ClaudeToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = inputSchema
            });
        }
        return definitions;
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

    private static List<ClaudeContentBlock> ExtractToolUseBlocks(ClaudeMessagesResponse response) =>
        response.Content is null
            ? []
            : [.. response.Content.Where(b => string.Equals(b.Type, "tool_use", StringComparison.OrdinalIgnoreCase))];

    private static string? ExtractThinkingText(ClaudeMessagesResponse response)
    {
        if (response.Content is null)
        {
            return null;
        }

        StringBuilder sb = new();
        foreach (ClaudeContentBlock block in response.Content)
        {
            if (string.Equals(block.Type, "thinking", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(block.Thinking))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(block.Thinking);
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static string ExtractTextContent(ClaudeMessagesResponse response)
    {
        if (response.Content is null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        foreach (ClaudeContentBlock block in response.Content)
        {
            if (string.Equals(block.Type, "text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(block.Text))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(block.Text);
            }
        }
        return sb.ToString();
    }
}
