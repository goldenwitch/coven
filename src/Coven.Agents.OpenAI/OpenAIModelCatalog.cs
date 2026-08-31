// SPDX-License-Identifier: BUSL-1.1

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Lists models from the OpenAI Models API.
/// </summary>
/// <remarks>
/// The endpoint returns every model on the account — embeddings, audio, image, moderation —
/// with nothing in the payload marking which can hold a conversation. Filtering is therefore an
/// <b>allowlist</b> of chat families rather than a denylist of known non-chat models: a denylist
/// fails open, letting a newly released embedding model appear as a chat option, and needs
/// editing on every release. An allowlist fails closed into the visible "other" group.
/// </remarks>
/// <param name="httpClient">The client to use.</param>
/// <param name="ownsClient">Whether disposing the catalog should dispose the client.</param>
public sealed class OpenAIModelCatalog(HttpClient httpClient, bool ownsClient = false) : IModelCatalog, IDisposable
{
    private const string DefaultEndpoint = "https://api.openai.com";

    private static readonly JsonSerializerOptions _serializerOptions = new();

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly bool _ownsClient = ownsClient;

    /// <summary>Creates a catalog with its own <see cref="HttpClient"/>.</summary>
    public OpenAIModelCatalog()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, ownsClient: true)
    {
    }

    /// <inheritdoc />
    public string ProviderName => "OpenAI";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelDescriptor>> ListAsync(
        ModelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("An OpenAI API key is required to list models.");
        }

        string baseUrl = request.Endpoint?.ToString().TrimEnd('/') ?? DefaultEndpoint;

        using HttpRequestMessage httpRequest = new(HttpMethod.Get, $"{baseUrl}/v1/models");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"OpenAI models request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}",
                null,
                response.StatusCode);
        }

        string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        OpenAIModelListResponse? parsed = JsonSerializer.Deserialize<OpenAIModelListResponse>(payload, _serializerOptions);

        if (parsed?.Data is null)
        {
            return [];
        }

        List<ModelDescriptor> models = [];
        foreach (OpenAIModelItem item in parsed.Data)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            ModelFamilyRule rule = ModelFamilies.Resolve(item.Id);
            if (!rule.IsChatModel)
            {
                continue;
            }

            DateTimeOffset? created = item.Created > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.Created)
                : null;

            models.Add(new ModelDescriptor(
                Id: item.Id,
                DisplayName: item.Id,
                Family: rule.Family,
                Created: created,
                ContextWindow: null,
                Capabilities: rule.Capabilities));
        }

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

    private sealed record OpenAIModelListResponse
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<OpenAIModelItem>? Data { get; init; }
    }

    private sealed record OpenAIModelItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("created")]
        public long Created { get; init; }
    }
}
