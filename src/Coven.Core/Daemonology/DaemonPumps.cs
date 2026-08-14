// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Daemonology;

/// <summary>
/// Helpers for supervising the long-running pump tasks a session owns.
/// </summary>
public static class DaemonPumps
{
    /// <summary>
    /// Completes when every pump completes, but faults as soon as <b>any one</b> of them faults.
    /// </summary>
    /// <param name="pumps">The pump tasks to supervise.</param>
    /// <returns>A task that surfaces the first fault immediately.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="Task.WhenAll(Task[])"/> is the wrong tool for supervising pumps. It does not
    /// complete until <i>all</i> tasks finish, and a journal-tailing pump only finishes on
    /// cancellation. A session whose gateway pump faults while its tailing pumps keep running
    /// therefore never observes the fault — the daemon never fails, the ritual never learns,
    /// and the caller waits forever on a turn that is already dead.
    /// </para>
    /// <para>
    /// This waits on each completion in turn and re-awaits it, so a faulted pump throws at the
    /// moment it faults while normal completion still waits for the rest.
    /// </para>
    /// </remarks>
    public static async Task WhenAllOrFirstFault(params Task[] pumps)
    {
        ArgumentNullException.ThrowIfNull(pumps);

        List<Task> remaining = [.. pumps];
        while (remaining.Count > 0)
        {
            Task completed = await Task.WhenAny(remaining).ConfigureAwait(false);

            // Re-awaiting surfaces a fault or cancellation without waiting on the others.
            await completed.ConfigureAwait(false);

            _ = remaining.Remove(completed);
        }
    }
}
