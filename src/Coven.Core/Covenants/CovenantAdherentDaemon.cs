// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Daemonology;

namespace Coven.Core.Covenants;

/// <summary>
/// A daemon that executes covenant routes at runtime by tailing source journals,
/// applying route transformations, and writing results to target journals.
/// </summary>
internal sealed class CovenantAdherentDaemon(
    IScrivener<DaemonEvent> daemonEvents,
    CovenantDescriptor covenant,
    IServiceProvider services) : ContractDaemon(daemonEvents)
{
    private readonly CovenantDescriptor _covenant = covenant;
    private readonly IServiceProvider _services = services;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    public override async Task Start(CancellationToken cancellationToken = default)
    {
        if (!await Transition(Status.Running, cancellationToken))
        {
            return; // Already running (idempotent)
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pumpTask = RunPumpsAsync(_cts.Token);
    }

    public override async Task Shutdown(CancellationToken cancellationToken = default)
    {
        if (!await Transition(Status.Completed, cancellationToken))
        {
            return; // Already completed (idempotent)
        }

        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_pumpTask is not null)
            {
                await _pumpTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task RunPumpsAsync(CancellationToken ct)
    {
        Task[] tasks = [.. _covenant.Pumps.Select(pump => pump.CreatePump(_services, ct))];

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown — not an error.
        }
        catch (Exception)
        {
            // At least one pump faulted. Cancel all remaining pumps.
            if (_cts is not null)
            {
                await _cts.CancelAsync();
            }

            // Surface the first failure through the daemon event journal.
            Exception? firstFault = tasks
                .Where(t => t.IsFaulted)
                .Select(t => t.Exception!.InnerException ?? t.Exception)
                .FirstOrDefault();

            if (firstFault is not null)
            {
                await Fail(firstFault, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
