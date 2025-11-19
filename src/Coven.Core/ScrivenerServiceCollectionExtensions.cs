// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Core;

/// <summary>
/// IServiceCollection extensions for registering typed scriveners.
/// Hides keyed registrations; callers use simple generic methods.
/// </summary>
public static class ScrivenerServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed scrivener using the provided builder configuration.
    /// Uses Scoped lifetime by default.
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional builder configuration (e.g., transport).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddScrivener<TEntry>(this IServiceCollection services, Action<ScrivenerBuilder>? configure = null)
        where TEntry : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped(sp =>
        {
            ScrivenerBuilder builder = new(sp);
            configure?.Invoke(builder);
            return builder.ForEntry<TEntry>().Build();
        });
        return services;
    }

    /// <summary>
    /// Registers a typed scrivener if one isn't already present, using the provided builder configuration.
    /// Uses Scoped lifetime by default.
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional builder configuration (e.g., transport).</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection TryAddScrivener<TEntry>(this IServiceCollection services, Action<ScrivenerBuilder>? configure = null)
        where TEntry : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped(sp =>
        {
            ScrivenerBuilder builder = new(sp);
            configure?.Invoke(builder);
            return builder.ForEntry<TEntry>().Build();
        });
        return services;
    }
}
