// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coven.Chat;

/// <summary>
/// Minimal journal API bound to a single journal stream (binding handled by DI).
/// <para>
/// TJournalEntryType is the message/entry type your app writes/reads (often a union/base + derived records).
/// No correlation id is exposed here; correlation belongs to the binding that provides this instance.
/// </para>
/// </summary>
/// <typeparam name="TJournalEntryType">The typed entry that is appended to and read from the journal.</typeparam>
public interface IScrivener<TJournalEntryType> where TJournalEntryType : notnull
{
    /// <summary>
    /// Append one entry; returns the assigned journal position for chaining/awaits.
    /// </summary>
    /// <param name="entry">The entry to append.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The monotonically increasing journal position assigned to the entry.</returns>
    Task<long> WriteAsync(TJournalEntryType entry, CancellationToken ct = default);

    /// <summary>
    /// Stream entries with <c>journalPosition &gt; afterPosition</c> (forward).
    /// </summary>
    /// <param name="afterPosition">Only entries strictly after this position are returned.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async sequence of (journalPosition, entry) pairs.</returns>
    IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> TailAsync(long afterPosition = 0, CancellationToken ct = default);

    /// <summary>
    /// Stream entries with <c>journalPosition &lt; beforePosition</c> in descending order (backward).
    /// </summary>
    /// <param name="beforePosition">Only entries strictly before this position are returned; defaults to logical end.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async sequence of (journalPosition, entry) pairs in descending position order.</returns>
    IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken ct = default);

    /// <summary>
    /// Wait for the next entry after <paramref name="afterPosition"/> that matches the predicate.
    /// </summary>
    /// <param name="afterPosition">Only consider entries strictly after this position.</param>
    /// <param name="match">Predicate to select the desired entry.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The first matching (journalPosition, entry) pair.</returns>
    Task<(long journalPosition, TJournalEntryType entry)> WaitForAsync(long afterPosition, System.Func<TJournalEntryType, bool> match, CancellationToken ct = default);

    /// <summary>
    /// Convenience overload: wait for the next entry of a specific derived type.
    /// </summary>
    /// <typeparam name="TDerived">The derived entry type to match.</typeparam>
    /// <param name="afterPosition">Only consider entries strictly after this position.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The first matching (journalPosition, entry) pair with the derived entry.</returns>
    Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, CancellationToken ct = default)
        where TDerived : TJournalEntryType;
}