// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Coven.Spellcasting.Agents;

/// <summary>
/// Abstraction for a tail multiplexer that can emit tail events and accept posted input.
/// Implementations may be asymmetric (e.g., read from one source, write to another).
/// </summary>
public interface ITailMux : IAsyncDisposable
{
    /// <summary>
    /// Tails the underlying source and invokes <paramref name="onMessage"/> for each event.
    /// Stops when canceled or the underlying source completes/exits.
    /// Exactly one active tailer is supported at a time unless otherwise documented.
    /// </summary>
    Task TailAsync(Func<TailEvent, ValueTask> onMessage, CancellationToken ct = default);

    /// <summary>
    /// Posts a single line/message into the underlying sink (if supported by the implementation).
    /// </summary>
    Task WriteLineAsync(string line, CancellationToken ct = default);
}
