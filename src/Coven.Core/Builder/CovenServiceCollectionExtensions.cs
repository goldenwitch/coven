// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Builder;

/// <summary>
/// DI entry points for composing and finalizing a Coven runtime.
/// </summary>
public static class CovenServiceCollectionExtensions
{
    /// <summary>
    /// Composes a Coven using the provided builder action and ensures finalization.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="build">Callback to register MagikBlocks and options.</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection BuildCoven(this IServiceCollection services, Action<CovenServiceBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(build);

        CovenServiceBuilder builder = new(services);
        build(builder);
        // Idempotent finalize if user forgot to call Done()
        builder.Done();
        return services;
    }
}

