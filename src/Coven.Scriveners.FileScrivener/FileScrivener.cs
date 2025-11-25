// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// File-backed scrivener that delegates in-memory semantics to an internal <see cref="InMemoryScrivener{TEntry}"/>,
/// enabling snapshot-based flushing by a companion daemon. This type itself does not perform I/O.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public sealed class FileScrivener<TEntry>(IScrivener<TEntry> inner) : IScrivener<TEntry> where TEntry : notnull
{
    private readonly IScrivener<TEntry> _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public Task<long> WriteAsync(TEntry entry, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(entry, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<(long journalPosition, TEntry entry)> TailAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
        => _inner.TailAsync(afterPosition, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<(long journalPosition, TEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken cancellationToken = default)
        => _inner.ReadBackwardAsync(beforePosition, cancellationToken);

    /// <inheritdoc />
    public Task<(long journalPosition, TEntry entry)> WaitForAsync(long afterPosition, Func<TEntry, bool> match, CancellationToken cancellationToken = default)
        => _inner.WaitForAsync(afterPosition, match, cancellationToken);

    /// <inheritdoc />
    public Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default) where TDerived : TEntry
        => _inner.WaitForAsync(afterPosition, match, cancellationToken);
}
