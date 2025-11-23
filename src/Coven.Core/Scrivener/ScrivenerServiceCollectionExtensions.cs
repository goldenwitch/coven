// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Scrivener;

/// <summary>
/// IServiceProvider extensions for producing typed scriveners.
/// </summary>
public static class ScrivenerServiceProviderExtensions
{
    /// <summary>
    /// Builds a scrivener using fluent configuration.
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static ScrivenerFactory<TEntry> BuildScrivener<TEntry>(this IServiceProvider serviceProvider)
        where TEntry : notnull
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ScrivenerFactory<TEntry> builder = new(serviceProvider);
        return builder;
    }
}
