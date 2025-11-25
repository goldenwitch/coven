// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Serializes a journal entry (with its assigned position) into a persistable string representation.
/// Implementations should produce a single-line format when used with newline-delimited sinks.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public interface IEntrySerializer<TEntry>
{
    /// <summary>
    /// Serialize the provided entry and position into a string suitable for persistence.
    /// </summary>
    /// <param name="position">The assigned journal position for the entry.</param>
    /// <param name="entry">The entry payload.</param>
    /// <returns>Serialized string representation.</returns>
    string Serialize(long position, TEntry entry);
}
