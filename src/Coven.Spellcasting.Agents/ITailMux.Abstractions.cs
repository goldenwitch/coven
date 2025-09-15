// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Coven.Spellcasting.Agents;

/// <summary>
/// Read-only tailing capability abstraction.
/// </summary>
public interface ITailSource : IAsyncDisposable
{
    Task TailAsync(Func<TailEvent, ValueTask> onMessage, CancellationToken ct = default);
}

/// <summary>
/// Write-only sending capability abstraction.
/// </summary>
public interface ISendPort : IAsyncDisposable
{
    Task WriteAsync(string data, CancellationToken ct = default);
}

/// <summary>
/// Generic tail multiplexer composed from independent send and tail capabilities.
/// Implementations may be asymmetric and can specialize either side via TSend/TTail.
/// </summary>
// Note: A generic ITailMux<TSend,TTail> interface is unnecessary since
// BaseCompositeTailMux already exposes typed properties. Keep only ITailMux.
