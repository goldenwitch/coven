// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents;

/// <summary>
/// Capabilities a model is believed to support.
/// </summary>
/// <remarks>
/// Inferred from the model's family, not reported by provider list endpoints. Treat these as
/// hints for shaping a user interface; the provider's own error is authoritative.
/// </remarks>
[Flags]
public enum ModelCapabilities
{
    /// <summary>Nothing known.</summary>
    None = 0,

    /// <summary>Supports incremental token streaming.</summary>
    Streaming = 1,

    /// <summary>Supports tool or function calling.</summary>
    Tools = 2,

    /// <summary>Accepts image input.</summary>
    Vision = 4,

    /// <summary>Supports extended thinking or reasoning output.</summary>
    Thinking = 8
}

/// <summary>
/// A model offered by a provider.
/// </summary>
/// <param name="Id">Provider-native identifier sent on the wire.</param>
/// <param name="DisplayName">Human-readable label; falls back to <paramref name="Id"/>.</param>
/// <param name="Family">Inferred grouping key, such as <c>claude-sonnet</c> or <c>gpt</c>.</param>
/// <param name="Created">Publication timestamp when the provider reports one; drives newest-first ordering.</param>
/// <param name="ContextWindow">Input token limit when the provider reports one.</param>
/// <param name="Capabilities">Inferred capabilities. Advisory only.</param>
public sealed record ModelDescriptor(
    string Id,
    string DisplayName,
    string Family,
    DateTimeOffset? Created,
    int? ContextWindow,
    ModelCapabilities Capabilities);

/// <summary>
/// Credentials and endpoint for a catalog query.
/// </summary>
/// <param name="ApiKey">Provider API key. Ignored by catalogs that read local files.</param>
/// <param name="Endpoint">Optional base endpoint override.</param>
public sealed record ModelCatalogRequest(string? ApiKey = null, Uri? Endpoint = null);

/// <summary>
/// Lists the models a provider currently offers.
/// </summary>
/// <remarks>
/// Deliberately standalone rather than journal-backed: a catalog lookup is a query, not a
/// stream that benefits from replay. Implementations must be usable before a Coven session
/// exists, so a user interface can populate a picker while credentials are still being entered.
/// </remarks>
public interface IModelCatalog
{
    /// <summary>Provider name shown in a user interface.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Lists available models, newest first where the provider reports a timestamp.
    /// </summary>
    /// <param name="request">Credentials and endpoint.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The available models.</returns>
    Task<IReadOnlyList<ModelDescriptor>> ListAsync(
        ModelCatalogRequest request,
        CancellationToken cancellationToken = default);
}
