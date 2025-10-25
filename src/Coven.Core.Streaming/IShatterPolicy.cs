// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Defines a policy that shatters a single entry into zero or more entries.
/// Implementations should be pure and side‑effect free.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public interface IShatterPolicy<TEntry>
{
    /// <summary>
    /// Produces zero or more entries derived from the provided entry.
    /// Policies may return an empty sequence to indicate no change.
    /// </summary>
    /// <param name="entry">The source entry.</param>
    /// <returns>Zero or more entries to append.</returns>
    IEnumerable<TEntry> Shatter(TEntry entry);
}
