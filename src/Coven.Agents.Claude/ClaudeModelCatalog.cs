// SPDX-License-Identifier: BUSL-1.1

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coven.Agents.Claude;

/// <summary>
/// Lists models from the Anthropic Models API.
/// </summary>
/// <remarks>
/// Standalone by design: a user interface must be able to populate a model picker while the
/// API key is still being entered, before any Coven session exists.
/// </remarks>
/// <param name="httpClient">The client to use.</param>
/// <param name="ownsClient">Whether disposing the catalog should dispose the client.</param>
public sealed class ClaudeModelCatalog(HttpClient httpClient, bool ownsClient = false) : IModelCatalog, IDisposable
{
    private const string DefaultEndpoint = "https://api.anthropic.com";
    private const string AnthropicVersion = "2023-06-01";
    private const int PageSize = 1000;
    private const int MaxPages = 20;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly bool _ownsClient = ownsClient;

    /// <summary>Creates a catalog with its own <see cref="HttpClient"/>.</summary>
    public ClaudeModelCatalog()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, ownsClient: true)
    {
    }

    /// <inheritdoc />
    public string ProviderName => "Anthropic";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelDescriptor>> ListAsync(
        ModelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("An Anthropic API key is required to list models.");
        }

        string baseUrl = request.Endpoint?.ToString().TrimEnd('/') ?? DefaultEndpoint;
        List<ModelDescriptor> models = [];
        string? afterId = null;

        for (int page = 0; page < MaxPages; page++)
        {
            string url = $"{baseUrl}/v1/models?limit={PageSize}";
            if (afterId is not null)
            {
                url += $"&after_id={Uri.EscapeDataString(afterId)}";
            }

            using HttpRequestMessage httpRequest = new(HttpMethod.Get, url);
            httpRequest.Headers.Add("x-api-key", request.ApiKey);
            httpRequest.Headers.Add("anthropic-version", AnthropicVersion);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage response = await _httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Anthropic models request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}",
                    null,
                    response.StatusCode);
            }

            string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ClaudeModelListResponse? parsed = JsonSerializer.Deserialize<ClaudeModelListResponse>(payload, _serializerOptions);

            if (parsed?.Data is null || parsed.Data.Count == 0)
            {
                break;
            }

            foreach (ClaudeModelItem item in parsed.Data)
            {
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    continue;
                }

                ModelFamilyRule rule = ModelFamilies.Resolve(item.Id);
                models.Add(new ModelDescriptor(
                    Id: item.Id,
                    DisplayName: string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName,
                    Family: rule.Family,
                    Created: item.CreatedAt,
                    ContextWindow: null,
                    Capabilities: rule.Capabilities));
            }

            if (!parsed.HasMore || string.IsNullOrWhiteSpace(parsed.LastId))
            {
                break;
            }

            afterId = parsed.LastId;
        }

        // Every model the Anthropic endpoint returns is a chat model, so no filtering is needed.
        return [.. models.OrderByDescending(m => m.Created ?? DateTimeOffset.MinValue).ThenBy(m => m.Id, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed record ClaudeModelListResponse
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<ClaudeModelItem>? Data { get; init; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; init; }

        [JsonPropertyName("last_id")]
        public string? LastId { get; init; }
    }

    private sealed record ClaudeModelItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; init; }
    }
}
