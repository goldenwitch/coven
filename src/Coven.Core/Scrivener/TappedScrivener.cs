// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Scrivener;

/// <summary>
/// Base scrivener wrapper that exposes the underlying scrivener while delegating
/// read/tail/wait operations. Implementers override <see cref="WriteAsync(TEntry, CancellationToken)"/>
/// to perform side‑effects or routing, and can call <see cref="WriteInnerAsync(TEntry, CancellationToken)"/>
/// to append to the inner journal while preserving ordering semantics.
/// </summary>
/// <typeparam name="TEntry">The entry type for the journal.</typeparam>
public abstract class TappedScrivener<TEntry> : IScrivener<TEntry> where TEntry : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TappedScrivener{TEntry}"/> class.
    /// </summary>
    /// <param name="inner">The inner scrivener used for storage and read operations.</param>
    protected TappedScrivener(IScrivener<TEntry> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
    }

    /// <summary>
    /// Implement write behavior for the tapped scrivener. Implementations may call
    /// <see cref="WriteInnerAsync(TEntry, CancellationToken)"/> to append to the underlying journal
    /// before or after performing side‑effects.
    /// </summary>
    /// <param name="entry">The entry to append.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The assigned journal position.</returns>
    public abstract Task<long> WriteAsync(TEntry entry, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public IAsyncEnumerable<(long journalPosition, TEntry entry)> TailAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
        => Inner.TailAsync(afterPosition, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<(long journalPosition, TEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken cancellationToken = default)
        => Inner.ReadBackwardAsync(beforePosition, cancellationToken);

    /// <inheritdoc />
    public Task<(long journalPosition, TEntry entry)> WaitForAsync(long afterPosition, Func<TEntry, bool> match, CancellationToken cancellationToken = default)
        => Inner.WaitForAsync(afterPosition, match, cancellationToken);

    /// <inheritdoc />
    public Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default)
        where TDerived : TEntry
        => Inner.WaitForAsync(afterPosition, match, cancellationToken);

    /// <summary>Access the inner scrivener directly for advanced scenarios.</summary>
    protected IScrivener<TEntry> Inner { get; }

    /// <summary>
    /// Append to the inner scrivener while preserving ordering and position semantics.
    /// </summary>
    protected Task<long> WriteInnerAsync(TEntry entry, CancellationToken cancellationToken = default)
        => Inner.WriteAsync(entry, cancellationToken);
}
