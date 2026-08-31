// SPDX-License-Identifier: BUSL-1.1

using System.Text;

namespace Coven.Ui.Desktop.ViewModels;

/// <summary>
/// Accumulates the streaming fragments of one turn and closes when that turn is finalized.
/// </summary>
/// <remarks>
/// <para>
/// Chunks and the finalized response travel as two separate covenant routes, and each route
/// carries its own cursor. Nothing orders one against the other, so a fragment can reach the
/// interface <i>after</i> the finalized message it belongs to. Rendered naively that late
/// fragment opens a second streaming message underneath a response that has already been
/// completed.
/// </para>
/// <para>
/// This is the boundary that makes the turn, rather than the transport, decide what is still
/// renderable. Once <see cref="Close"/> has run, fragments are discarded: the finalized text
/// is authoritative and already contains everything the fragments were previewing, so nothing
/// is lost by dropping them.
/// </para>
/// </remarks>
internal sealed class StreamingTurn
{
    private readonly Lock _gate = new();
    private readonly StringBuilder _pending = new();
    private bool _closed;
    private bool _flushScheduled;

    /// <summary>Whether the turn has been finalized and no longer accepts fragments.</summary>
    public bool IsClosed
    {
        get
        {
            lock (_gate)
            {
                return _closed;
            }
        }
    }

    /// <summary>
    /// Begins a new turn, discarding anything left over from the last one.
    /// </summary>
    public void Open()
    {
        lock (_gate)
        {
            _pending.Clear();
            _closed = false;
            _flushScheduled = false;
        }
    }

    /// <summary>
    /// Offers a fragment to the current turn.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the caller should schedule a drain. False either because
    /// the turn is closed, or because a drain is already scheduled — one post per fragment
    /// would starve the interface on a long response.
    /// </returns>
    public bool Append(string text)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return false;
            }

            _pending.Append(text);

            if (_flushScheduled)
            {
                return false;
            }

            _flushScheduled = true;
            return true;
        }
    }

    /// <summary>
    /// Takes everything accumulated so far, or an empty string once the turn is closed.
    /// </summary>
    public string Drain()
    {
        lock (_gate)
        {
            _flushScheduled = false;

            if (_closed)
            {
                _pending.Clear();
                return string.Empty;
            }

            string pending = _pending.ToString();
            _pending.Clear();
            return pending;
        }
    }

    /// <summary>
    /// Finalizes the turn. Fragments already accumulated are dropped — the caller drains
    /// before closing — and later ones are refused.
    /// </summary>
    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
            _pending.Clear();
            _flushScheduled = false;
        }
    }
}
