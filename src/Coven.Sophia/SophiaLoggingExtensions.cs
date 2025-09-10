using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Coven.Sophia;

public static class SophiaLoggingExtensions
{
    // Register provider and options; storage must already be registered in DI.
    public static IServiceCollection AddSophiaLogging(
        this IServiceCollection services,
        SophiaLoggerOptions? options = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton(_ => options ?? new SophiaLoggerOptions());

        // Register provider as a singleton and into the ILoggerProvider collection without overriding existing ones
        services.TryAddSingleton<SophiaLoggerProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SophiaLoggerProvider>());

        // Do not call AddLogging here; let caller/Host configure logging to avoid clobbering configuration
        return services;
    }
}
