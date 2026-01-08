// SPDX-License-Identifier: BUSL-1.1

using System.Net.Http.Headers;
using System.Text;
using System.Globalization;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Gemini;

internal sealed class GeminiStreamingGatewayConnection(
    GeminiClientConfig configuration,
    [FromKeyedServices("Coven.InternalGeminiScrivener")] IScrivener<GeminiEntry> journal,
    ILogger<GeminiStreamingGatewayConnection> logger,
    IGeminiTranscriptBuilder transcriptBuilder,
    ITransmuter<GeminiClientConfig, GeminiRequestOptions> responseOptionsTransmuter) : IGeminiGatewayConnection, IAsyncDisposable
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly GeminiClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<GeminiEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IGeminiTranscriptBuilder _transcriptBuilder = transcriptBuilder ?? throw new ArgumentNullException(nameof(transcriptBuilder));
    private readonly ITransmuter<GeminiClientConfig, GeminiRequestOptions> _responseOptionsTransmuter = responseOptionsTransmuter ?? throw new ArgumentNullException(nameof(responseOptionsTransmuter));
    private readonly HttpClient _httpClient = CreateHttpClient(configuration);

    public Task ConnectAsync()
    {
        GeminiLog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(GeminiEfferent outgoing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<GeminiContent> contents = await _transcriptBuilder.BuildAsync(outgoing, _configuration.HistoryClip, cancellationToken).ConfigureAwait(false);
        GeminiLog.OutboundSendStart(_logger, contents.Count);

        GeminiRequestOptions options = await _responseOptionsTransmuter.Transmute(_configuration, cancellationToken).ConfigureAwait(false);

        string modelId = NormalizeModelId(_configuration.Model);
        GeminiGenerateContentRequest request = new()
        {
            Model = $"models/{modelId}",
            Contents = contents,
            GenerationConfig = options.Generation,
            SafetySettings = options.SafetySettings,
            SystemInstruction = options.SystemInstruction
        };

        string path = BuildPath(":streamGenerateContent");
        using HttpRequestMessage message = new(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, _serializerOptions), Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        string responseId = Guid.NewGuid().ToString("N");
        string model = _configuration.Model;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                line = line["data:".Length..].Trim();
            }
            if (string.Equals(line, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            GeminiGenerateContentResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(line, _serializerOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            GeminiLog.StreamLine(_logger, line);

            if (chunk?.Error is not null)
            {
                string code = chunk.Error.Code?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
                throw new HttpRequestException(
                    $"Gemini streaming error {chunk.Error.Status ?? "unknown"} ({code}): {chunk.Error.Message}",
                    null,
                    System.Net.HttpStatusCode.BadRequest);
            }

            if (!string.IsNullOrWhiteSpace(chunk?.ResponseId))
            {
                responseId = chunk!.ResponseId!;
            }
            if (!string.IsNullOrWhiteSpace(chunk?.ModelVersion))
            {
                model = chunk!.ModelVersion!;
            }

            if (chunk?.PromptFeedback is not null && !string.IsNullOrWhiteSpace(chunk.PromptFeedback.BlockReason))
            {
                string reason = chunk.PromptFeedback.BlockReason!;
                string? category = chunk.PromptFeedback.SafetyRatings?.FirstOrDefault()?.Category;
                GeminiLog.SafetyBlocked(_logger, reason, category);

                GeminiSafetyBlock safety = new("gemini", reason, responseId, timestamp, model);
                await _journal.WriteAsync(safety, cancellationToken).ConfigureAwait(false);
                break;
            }

            string textDelta = ConcatenateText(chunk);
            if (!string.IsNullOrEmpty(textDelta))
            {
                GeminiAfferentChunk afferentChunk = new(
                    Sender: "gemini",
                    Text: textDelta,
                    ResponseId: responseId,
                    Timestamp: timestamp,
                    Model: model);
                await _journal.WriteAsync(afferentChunk, cancellationToken).ConfigureAwait(false);
            }

            string reasoningDelta = ConcatenateReasoning(chunk);
            if (!string.IsNullOrEmpty(reasoningDelta))
            {
                GeminiAfferentReasoningChunk reasoning = new(
                    Sender: "gemini",
                    Text: reasoningDelta,
                    ResponseId: responseId,
                    Timestamp: timestamp,
                    Model: model);
                await _journal.WriteAsync(reasoning, cancellationToken).ConfigureAwait(false);
            }
        }

        GeminiStreamCompleted done = new(
            Sender: "gemini",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: model);
        await _journal.WriteAsync(done, cancellationToken).ConfigureAwait(false);

        GeminiLog.OutboundSendSucceeded(_logger);
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private string BuildPath(string method)
    {
        string baseUrl = _configuration.Endpoint?.ToString()?.TrimEnd('/') ?? "https://generativelanguage.googleapis.com";
        string model = Uri.EscapeDataString(NormalizeModelId(_configuration.Model));
        return $"{baseUrl}/v1beta/models/{model}{method}?key={Uri.EscapeDataString(_configuration.ApiKey)}&alt=sse";
    }

    private static HttpClient CreateHttpClient(GeminiClientConfig configuration)
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Coven.Gemini", "1.0"));
        if (configuration.Endpoint is not null)
        {
            client.BaseAddress = configuration.Endpoint;
        }
        return client;
    }

    private static string ConcatenateText(GeminiGenerateContentResponse? response)
    {
        if (response?.Candidates is null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        foreach (GeminiCandidate candidate in response.Candidates)
        {
            if (candidate.Content?.Parts is null)
            {
                continue;
            }

            foreach (GeminiPart part in candidate.Content.Parts)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    sb.Append(part.Text);
                }
            }
        }
        return sb.ToString();
    }

    private static string ConcatenateReasoning(GeminiGenerateContentResponse? response)
    {
        if (response?.Candidates is null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        foreach (GeminiCandidate candidate in response.Candidates)
        {
            if (candidate.Content?.Parts is null)
            {
                continue;
            }

            foreach (GeminiPart part in candidate.Content.Parts)
            {
                if (!string.IsNullOrEmpty(part.ModelReasoning))
                {
                    sb.Append(part.ModelReasoning);
                }
            }
        }
        return sb.ToString();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException($"Gemini request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}", null, response.StatusCode);
    }

    private static string NormalizeModelId(string model)
    {
        return string.IsNullOrWhiteSpace(model)
            ? model
            : model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? model["models/".Length..]
                : model;
    }
}
