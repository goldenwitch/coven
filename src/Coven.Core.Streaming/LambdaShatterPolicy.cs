// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Shatter policy backed by a provided delegate.
/// </summary>
/// <typeparam name="TEntry">The entry type to shatter.</typeparam>
/// <param name="shatter">Delegate that produces zero or more entries for a given input.</param>
public sealed class LambdaShatterPolicy<TEntry>(Func<TEntry, IEnumerable<TEntry>> shatter)
    : IShatterPolicy<TEntry>
{
    private readonly Func<TEntry, IEnumerable<TEntry>> _shatter = shatter ?? throw new ArgumentNullException(nameof(shatter));

    /// <inheritdoc />
    public IEnumerable<TEntry> Shatter(TEntry entry)
        => _shatter(entry);
}
