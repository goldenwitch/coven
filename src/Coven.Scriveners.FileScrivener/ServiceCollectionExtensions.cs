// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Daemonology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Dependency Injection helpers to register a file-backed scrivener, defaults, and the flusher daemon for a given entry type.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a file-backed scrivener for TEntry and its flushing daemon.
    /// Adds defaults for serializer, sink, and flush predicate (count threshold).
    /// Registers a `ContractDaemon` that performs tail→snapshot→flush behavior.
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="config">File scrivener configuration (file path, thresholds).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileScrivener<TEntry>(this IServiceCollection services, FileScrivenerConfig config)
        where TEntry : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        // Inner in-memory journal (scoped) used by the file scrivener
        services.AddKeyedScoped<IScrivener<TEntry>, InMemoryScrivener<TEntry>>("Coven.InternalFileScrivener");
        services.AddScoped<IScrivener<TEntry>>(sp =>
        {
            IScrivener<TEntry> inner = sp.GetRequiredKeyedService<IScrivener<TEntry>>("Coven.InternalFileScrivener");
            return new FileScrivener<TEntry>(inner);
        });

        // Serializer + sink defaults (TryAdd so hosts can override)
        services.TryAddScoped<IEntrySerializer<TEntry>, JsonEntrySerializer<TEntry>>();
        services.TryAddScoped<IFlushSink<TEntry>>(sp => new FileAppendFlushSink<TEntry>(config.FilePath, sp.GetRequiredService<IEntrySerializer<TEntry>>()));

        // Predicate default based on threshold
        services.TryAddScoped<IFlushPredicate<TEntry>>(_ => new CountThresholdFlushPredicate<TEntry>(config.FlushThreshold));

        // Daemon journal for status contracts
        services.TryAddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();

        // Flusher daemon
        services.AddScoped<ContractDaemon, FlusherDaemon<TEntry>>();

        return services;
    }
}
