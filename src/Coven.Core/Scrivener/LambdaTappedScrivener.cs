// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Scrivener;

/// <summary>
/// A tapped scrivener that uses a delegate to implement write behavior.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="LambdaTappedScrivener{TEntry}"/>.
/// </remarks>
/// <param name="inner">The inner scrivener used for storage and reads.</param>
/// <param name="write">Optional delegate that performs the write; receives the entry, the inner scrivener, and a token. Defaults to pass‑through to the inner.</param>
public sealed class LambdaTappedScrivener<TEntry>(
    IScrivener<TEntry> inner,
    Func<TEntry, IScrivener<TEntry>, CancellationToken, Task<long>>? write) : TappedScrivener<TEntry>(inner) where TEntry : notnull
{
    private readonly Func<TEntry, IScrivener<TEntry>, CancellationToken, Task<long>> _write = write ?? ((entry, innerScrivener, ct) => innerScrivener.WriteAsync(entry, ct));


    /// <inheritdoc />
    public override Task<long> WriteAsync(TEntry entry, CancellationToken cancellationToken = default)
        => _write(entry, Inner, cancellationToken);
}
