// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Covenants;

/// <summary>
/// Extension methods for registering covenants with dependency injection.
/// </summary>
public static class CovenantServiceCollectionExtensions
{
    /// <summary>
    /// Registers a covenant with the service collection.
    /// The builder collects metadata for static analysis while wiring DI registrations.
    /// </summary>
    /// <typeparam name="TCovenant">The covenant type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Callback to configure the covenant's entry flow graph.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at registration time if the covenant graph is incomplete or disconnected.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddCovenant&lt;ChatCovenant&gt;(covenant =>
    /// {
    ///     covenant.Source&lt;UserMessage&gt;();
    ///     covenant.Sink&lt;AssistantMessage&gt;();
    ///     
    ///     covenant.Window&lt;ChatChunk, ChatEfferent&gt;(
    ///         policy: new ParagraphWindowPolicy&lt;ChatChunk&gt;(),
    ///         transmuter: new ChatChunkBatchTransmuter());
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddCovenant<TCovenant>(
        this IServiceCollection services,
        Action<IStreamingCovenantBuilder<TCovenant>> configure)
        where TCovenant : ICovenant
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        StreamingCovenantBuilder<TCovenant> builder = new(services);
        configure(builder);
        builder.Validate();

        // Register the graph for runtime inspection if needed
        services.AddSingleton(builder.Graph);

        return services;
    }
}
