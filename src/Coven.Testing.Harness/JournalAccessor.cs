// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Testing.Harness;

/// <summary>
/// Provides access to registered journals for test inspection and assertions.
/// </summary>
public sealed class JournalAccessor(IServiceProvider services)
{
    private readonly IServiceProvider _services = services;

    /// <summary>
    /// Gets a scrivener for the specified entry type.
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <returns>The scrivener for the entry type.</returns>
    /// <exception cref="InvalidOperationException">No scrivener registered for the entry type.</exception>
    public IScrivener<TEntry> Get<TEntry>() where TEntry : notnull
    {
        return _services.GetRequiredService<IScrivener<TEntry>>();
    }

    /// <summary>
    /// Tries to get a scrivener for the specified entry type.
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <returns>The scrivener, or null if not registered.</returns>
    public IScrivener<TEntry>? TryGet<TEntry>() where TEntry : notnull
    {
        return _services.GetService<IScrivener<TEntry>>();
    }

    /// <summary>
    /// Reads all entries from a journal using backward read (non-blocking).
    /// </summary>
    /// <typeparam name="TEntry">The journal entry type.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All entries in chronological order.</returns>
    public async Task<IReadOnlyList<TEntry>> GetEntriesAsync<TEntry>(CancellationToken cancellationToken = default)
        where TEntry : notnull
    {
        IScrivener<TEntry> scrivener = Get<TEntry>();
        List<TEntry> entries = [];

        await foreach ((long _, TEntry entry) in scrivener.ReadBackwardAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            entries.Add(entry);
        }

        // Reverse to get chronological order
        entries.Reverse();
        return entries;
    }

    /// <summary>
    /// Gets all entries of a specific derived type from a journal.
    /// </summary>
    /// <typeparam name="TEntry">The base journal entry type.</typeparam>
    /// <typeparam name="TDerived">The derived entry type to filter for.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All entries of the derived type.</returns>
    public async Task<IReadOnlyList<TDerived>> GetEntriesAsync<TEntry, TDerived>(CancellationToken cancellationToken = default)
        where TEntry : notnull
        where TDerived : TEntry
    {
        IReadOnlyList<TEntry> entries = await GetEntriesAsync<TEntry>(cancellationToken).ConfigureAwait(false);
        return [.. entries.OfType<TDerived>()];
    }
}
