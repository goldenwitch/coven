// SPDX-License-Identifier: BUSL-1.1
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Core;

/// <summary>
/// DI helpers for registering in-memory scriveners.
/// </summary>
public static class InMemoryScrivenerServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton in-memory scrivener for the given entry type.
    /// Uses TryAdd to avoid duplicate registrations.
    /// </summary>
    public static IServiceCollection AddInMemoryScrivener<TEntry>(this IServiceCollection services)
        where TEntry : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IScrivener<TEntry>, InMemoryScrivener<TEntry>>();
        return services;
    }
}
