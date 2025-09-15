// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Coven.Spellcasting.Agents;

/// <summary>
/// Optional convenience methods for line-oriented writes on top of ITailMux.
/// Core contracts intentionally expose only WriteAsync to keep framing decisions external.
/// </summary>
public static class TailMuxWriteExtensions
{
    /// <summary>
    /// Writes the provided data followed by <see cref="Environment.NewLine"/> using <see cref="ITailMux.WriteAsync"/>.
    /// </summary>
    public static Task WriteLineAsync(this ITailMux mux, string data, CancellationToken ct = default)
    {
        if (mux is null) throw new ArgumentNullException(nameof(mux));
        if (data is null) throw new ArgumentNullException(nameof(data));
        return mux.WriteAsync(data + Environment.NewLine, ct);
    }
}

