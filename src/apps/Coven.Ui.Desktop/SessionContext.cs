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

    /// <summary>Subscribes to session start failures.</summary>
    public void SubscribeToFailure(Action<Exception> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            _failureHandlers.Add(handler);
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

    /// <summary>Clears the current journal so a rebuild does not replay a dead one.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
        }
    }

    /// <summary>Reports that a session could not start.</summary>
    public void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Action<Exception>[] handlers;
        lock (_gate)
        {
            handlers = [.. _failureHandlers];
        }

        foreach (Action<Exception> handler in handlers)
        {
            handler(error);
        }
    }
}
