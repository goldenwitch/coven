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
        IEnumerable<Task> tasks = _covenant.Pumps.Select(pump => pump.CreatePump(_services, ct));
        await Task.WhenAll(tasks);
    }
}
