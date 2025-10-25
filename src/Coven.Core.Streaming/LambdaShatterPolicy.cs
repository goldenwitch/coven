// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

public sealed class LambdaShatterPolicy<TEntry>(Func<TEntry, IEnumerable<TEntry>> shatter)
    : IShatterPolicy<TEntry>
{
    private readonly Func<TEntry, IEnumerable<TEntry>> _shatter = shatter ?? throw new ArgumentNullException(nameof(shatter));

    public IEnumerable<TEntry> Shatter(TEntry entry)
        => _shatter(entry);
}
