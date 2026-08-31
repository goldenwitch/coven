// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Ui.Shell;

namespace Coven.Ui.Desktop;

/// <summary>
/// Long-lived hand-off point between ritual scopes and the user interface.
/// </summary>
/// <remarks>
/// <para>
/// Journals are scoped to a ritual and <c>CovenExecutionScope.CurrentProvider</c> is internal,
/// so the only supported way to reach them is constructor injection into a block.
/// <see cref="UiHostBlock"/> runs inside the scope and publishes them here.
/// </para>
/// <para>
/// This outlives any single session: rebuilding for a new provider or API key produces a new
/// scope and therefore a <b>new journal instance</b>, which subscribers must pick up.
/// Late subscribers are replayed the current journal, because the session starts on a
/// background task and may publish before the view model exists.
/// </para>
/// </remarks>
internal sealed class SessionContext
{
    private readonly Lock _gate = new();
    private readonly List<Action<IScrivener<UiEntry>>> _journalHandlers = [];
    private readonly List<Action<Exception>> _failureHandlers = [];

    private IScrivener<UiEntry>? _current;
    private Exception? _failure;

    /// <summary>
    /// The journal published by the running ritual, or <see langword="null"/> before one
    /// exists. This is the only instance the interface is tailing; anything written elsewhere
    /// is never seen.
    /// </summary>
    public IScrivener<UiEntry>? Journal
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Subscribes to journal publication. If a journal is already live, the handler is
    /// invoked immediately.
    /// </summary>
    public void SubscribeToJournal(Action<IScrivener<UiEntry>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        IScrivener<UiEntry>? existing;
        lock (_gate)
        {
            _journalHandlers.Add(handler);
            existing = _current;
        }

        if (existing is not null)
        {
            handler(existing);
        }
    }

    /// <summary>
    /// Subscribes to session failures. If one has already been reported, the handler is
    /// invoked immediately.
    /// </summary>
    /// <remarks>
    /// The replay is the point. The session is started on a background task before the view
    /// model exists, so a failure that happens quickly — a bad key, a model that will not
    /// load — is otherwise delivered to an empty handler list and lost, leaving a window that
    /// looks idle rather than broken.
    /// </remarks>
    public void SubscribeToFailure(Action<Exception> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        Exception? existing;
        lock (_gate)
        {
            _failureHandlers.Add(handler);
            existing = _failure;
        }

        if (existing is not null)
        {
            handler(existing);
        }
    }

    /// <summary>Called from inside the ritual scope by <see cref="UiHostBlock"/>.</summary>
    public void Publish(IScrivener<UiEntry> shellJournal)
    {
        ArgumentNullException.ThrowIfNull(shellJournal);

        Action<IScrivener<UiEntry>>[] handlers;
        lock (_gate)
        {
            _current = shellJournal;
            handlers = [.. _journalHandlers];
        }

        foreach (Action<IScrivener<UiEntry>> handler in handlers)
        {
            handler(shellJournal);
        }
    }

    /// <summary>
    /// Clears the current journal and any recorded failure, so a rebuild neither replays a
    /// dead journal nor inherits the previous session's error.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
            _failure = null;
        }
    }

    /// <summary>Reports that a session failed, and retains it for late subscribers.</summary>
    public void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Action<Exception>[] handlers;
        lock (_gate)
        {
            // First one wins: later faults are usually consequences of the first, and the
            // original is the one that explains the session.
            _failure ??= error;
            handlers = [.. _failureHandlers];
        }

        foreach (Action<Exception> handler in handlers)
        {
            handler(error);
        }
    }
}
