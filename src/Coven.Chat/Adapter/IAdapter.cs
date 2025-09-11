// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Adapter;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Transport adapter contract for a specific protocol type T.
/// - ReadAsync: yields protocol entries from the external system.
/// - DeliverAsync: delivers protocol entries to the external system; adapters may ignore entries they don't handle.
/// </summary>
/// <typeparam name="T">Protocol record type stored in the journal.</typeparam>
public interface IAdapter<T> where T : notnull
{
    /// <summary>
    /// Stream protocol entries originating from the external system.
    /// The host appends each yielded entry to the journal.
    /// </summary>
    IAsyncEnumerable<T> ReadAsync(CancellationToken ct = default);

    /// <summary>
    /// Deliver a protocol entry to the external system.
    /// Implementations should be tolerant of entries they don't handle (no-op fast path).
    /// </summary>
    Task DeliverAsync(T entry, CancellationToken ct = default);
}
