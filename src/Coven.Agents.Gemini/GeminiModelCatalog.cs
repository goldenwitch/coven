// SPDX-License-Identifier: BUSL-1.1

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coven.Agents.Gemini;

/// <summary>
/// Lists models from the Gemini API.
/// </summary>
/// <remarks>
/// Gemini is the only provider that reports what each model can actually do:
/// <c>supportedGenerationMethods</c> tells us whether a model can hold a conversation, so
/// chat filtering here is an authoritative check rather than the family-prefix heuristic the
/// other catalogs fall back on. It also reports <c>inputTokenLimit</c>, so context windows
/// come from the API instead of being guessed.
/// </remarks>
/// <param name="httpClient">The client to use.</param>
/// <param name="ownsClient">Whether disposing the catalog should dispose the client.</param>
public sealed class GeminiModelCatalog(HttpClient httpClient, bool ownsClient = false) : IModelCatalog, IDisposable
{
    private const string DefaultEndpoint = "https://generativelanguage.googleapis.com";
    private const string ChatGenerationMethod = "generateContent";
    private const string ModelNamePrefix = "models/";
    private const int PageSize = 1000;
    private const int MaxPages = 20;

    private static readonly JsonSerializerOptions _serializerOptions = new();

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly bool _ownsClient = ownsClient;

    /// <summary>Creates a catalog with its own <see cref="HttpClient"/>.</summary>
    public GeminiModelCatalog()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, ownsClient: true)
    {
    }

    /// <inheritdoc />
    public string ProviderName => "Google Gemini";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelDescriptor>> ListAsync(
        ModelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("A Gemini API key is required to list models.");
        }

        string baseUrl = request.Endpoint?.ToString().TrimEnd('/') ?? DefaultEndpoint;
        List<ModelDescriptor> models = [];
        string? pageToken = null;

        for (int page = 0; page < MaxPages; page++)
        {
            // Gemini authenticates with a query-string key rather than a header.
            string url = $"{baseUrl}/v1beta/models?pageSize={PageSize}&key={Uri.EscapeDataString(request.ApiKey)}";
            if (pageToken is not null)
            {
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            using HttpRequestMessage httpRequest = new(HttpMethod.Get, url);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage response = await _httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Gemini models request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}",
                    null,
                    response.StatusCode);
            }

            string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            GeminiModelListResponse? parsed = JsonSerializer.Deserialize<GeminiModelListResponse>(payload, _serializerOptions);

            if (parsed?.Models is null || parsed.Models.Count == 0)
            {
                break;
            }

            foreach (GeminiModelItem item in parsed.Models)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    continue;
                }

                // The provider reports chat capability directly — no heuristic needed.
                if (item.SupportedGenerationMethods is null ||
                    !item.SupportedGenerationMethods.Contains(ChatGenerationMethod, StringComparer.Ordinal))
                {
                    continue;
                }

                string id = item.Name.StartsWith(ModelNamePrefix, StringComparison.Ordinal)
                    ? item.Name[ModelNamePrefix.Length..]
                    : item.Name;

                ModelFamilyRule rule = ModelFamilies.Resolve(id);
                models.Add(new ModelDescriptor(
                    Id: id,
                    DisplayName: string.IsNullOrWhiteSpace(item.DisplayName) ? id : item.DisplayName,
                    Family: rule.Family,
                    Created: null,
                    ContextWindow: item.InputTokenLimit > 0 ? item.InputTokenLimit : null,
                    Capabilities: rule.Capabilities));
            }

            if (string.IsNullOrWhiteSpace(parsed.NextPageToken))
            {
                break;
            }

            pageToken = parsed.NextPageToken;
        }

        // Gemini reports no creation timestamp, so descending id is the closest stand-in for
        // newest-first: version-numbered ids sort the recent families to the top.
        return [.. models.OrderByDescending(m => m.Id, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed record GeminiModelListResponse
    {
        [JsonPropertyName("models")]
        public IReadOnlyList<GeminiModelItem>? Models { get; init; }

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }
    }

    private sealed record GeminiModelItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("inputTokenLimit")]
        public int InputTokenLimit { get; init; }

        [JsonPropertyName("supportedGenerationMethods")]
        public IReadOnlyList<string>? SupportedGenerationMethods { get; init; }
    }
}
