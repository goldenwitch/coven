// SPDX-License-Identifier: BUSL-1.1

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Coven.Core;
using Coven.Gemini.Client;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Gemini;

internal sealed class GeminiRequestGatewayConnection(
    GeminiClientConfig configuration,
    [FromKeyedServices("Coven.InternalGeminiScrivener")] IScrivener<GeminiEntry> journal,
    ILogger<GeminiRequestGatewayConnection> logger,
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

        string path = BuildPath(":generateContent");
        using HttpRequestMessage message = new(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, _serializerOptions), Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        GeminiGenerateContentResponse? body = await JsonSerializer.DeserializeAsync<GeminiGenerateContentResponse>(responseStream, _serializerOptions, cancellationToken).ConfigureAwait(false);

        string responseId = string.IsNullOrWhiteSpace(body?.ResponseId) ? Guid.NewGuid().ToString("N") : body!.ResponseId!;
        string model = string.IsNullOrWhiteSpace(body?.ModelVersion) ? _configuration.Model : body!.ModelVersion!;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        if (body?.PromptFeedback is not null && !string.IsNullOrWhiteSpace(body.PromptFeedback.BlockReason))
        {
            string reason = body.PromptFeedback.BlockReason!;
            string? category = body.PromptFeedback.SafetyRatings?.FirstOrDefault()?.Category;
            GeminiLog.SafetyBlocked(_logger, reason, category);

            GeminiSafetyBlock safety = new("gemini", reason, responseId, timestamp, model);
            await _journal.WriteAsync(safety, cancellationToken).ConfigureAwait(false);
            return;
        }

        string text = ConcatenateText(body);
        if (!string.IsNullOrEmpty(text))
        {
            GeminiAfferent incoming = new(
                Sender: "gemini",
                Text: text,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: model);
            await _journal.WriteAsync(incoming, cancellationToken).ConfigureAwait(false);
        }

        string reasoning = ConcatenateReasoning(body);
        if (!string.IsNullOrEmpty(reasoning))
        {
            GeminiThought thought = new(
                Sender: "gemini",
                Text: reasoning,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: model);
            await _journal.WriteAsync(thought, cancellationToken).ConfigureAwait(false);
        }

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
        return $"{baseUrl}/v1beta/models/{model}{method}?key={Uri.EscapeDataString(_configuration.ApiKey)}";
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
