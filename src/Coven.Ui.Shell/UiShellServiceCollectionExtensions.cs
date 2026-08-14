// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Ui.Shell;

/// <summary>
/// Dependency Injection helpers for the application shell journal.
/// </summary>
public static class UiShellServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shell journal if the host has not supplied one.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddUiShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IScrivener<UiEntry>, InMemoryScrivener<UiEntry>>();
        return services;
    }
}
