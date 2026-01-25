// SPDX-License-Identifier: BUSL-1.1

using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coven.Gemini.Client;

/// <summary>
/// Lightweight REST client for Gemini generateContent / streamGenerateContent.
/// </summary>
public sealed class GeminiRestClient : IDisposable
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly GeminiRestClientOptions _options;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Creates a new Gemini REST client.
    /// </summary>
    /// <param name="options">Client options.</param>
    public GeminiRestClient(GeminiRestClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required.", nameof(options));
        }
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new ArgumentException("Model is required.", nameof(options));
        }

        _options = options;
        _httpClient = CreateHttpClient(options);
    }

    /// <summary>
    /// Creates a new Gemini REST client with a custom HttpClient.
    /// </summary>
    /// <param name="options">Client options.</param>
    /// <param name="httpClient">Pre-configured HttpClient; ownership is transferred to this instance.</param>
    public GeminiRestClient(GeminiRestClientOptions options, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required.", nameof(options));
        }
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new ArgumentException("Model is required.", nameof(options));
        }

        _options = options;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sends a generateContent request and returns the full response.
    /// </summary>
    /// <param name="request">Request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Gemini response.</returns>
    public async Task<GeminiGenerateContentResponse> GenerateContentAsync(
        GeminiGenerateContentRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        string path = BuildPath(":generateContent");
        using HttpRequestMessage message = new(HttpMethod.Post, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, _serializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        using HttpResponseMessage response = await _httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using Stream responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        GeminiGenerateContentResponse? body = await JsonSerializer
            .DeserializeAsync<GeminiGenerateContentResponse>(responseStream, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return body ?? new GeminiGenerateContentResponse();
    }

    /// <summary>
    /// Sends a streamGenerateContent request and yields response chunks.
    /// </summary>
    /// <param name="request">Request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of response chunks.</returns>
    public async IAsyncEnumerable<GeminiGenerateContentResponse> StreamGenerateContentAsync(
        GeminiGenerateContentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        string path = BuildPath(":streamGenerateContent") + "&alt=sse";
        using HttpRequestMessage message = new(HttpMethod.Post, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, _serializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        using HttpResponseMessage response = await _httpClient
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using Stream responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

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

            if (chunk is not null)
            {
                yield return chunk;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _httpClient.Dispose();
        _disposed = true;
    }

    private string BuildPath(string method)
    {
        string baseUrl = _options.Endpoint?.ToString()?.TrimEnd('/') ?? "https://generativelanguage.googleapis.com";
        string model = Uri.EscapeDataString(NormalizeModelId(_options.Model));
        return $"{baseUrl}/v1beta/models/{model}{method}?key={Uri.EscapeDataString(_options.ApiKey)}";
    }

    private static HttpClient CreateHttpClient(GeminiRestClientOptions options)
    {
        HttpClient client = new()
        {
            Timeout = options.Timeout
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Coven.Gemini.Client", "1.0"));
        if (options.Endpoint is not null)
        {
            client.BaseAddress = options.Endpoint;
        }
        return client;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException(
            $"Gemini request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
            null,
            response.StatusCode);
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
