// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// A daemon that executes covenant routes at runtime by tailing source journals,
/// applying route transformations, and writing results to target journals.
/// </summary>
internal sealed class CovenantAdherentDaemon(
    CovenantDescriptor covenant,
    IServiceProvider services) : IDaemon
{
    private readonly CovenantDescriptor _covenant = covenant;
    private readonly IServiceProvider _services = services;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    public Status Status { get; private set; } = Status.Stopped;

    public Task Start(CancellationToken cancellationToken = default)
    {
        if (Status != Status.Stopped)
        {
            throw new InvalidOperationException($"Cannot start daemon in state {Status}.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pumpTask = RunPumpsAsync(_cts.Token);
        Status = Status.Running;
        return Task.CompletedTask;
    }

    public async Task Shutdown(CancellationToken cancellationToken = default)
    {
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
        Status = Status.Completed;
    }

    private async Task RunPumpsAsync(CancellationToken ct)
    {
        IEnumerable<Task> tasks = _covenant.Pumps.Select(pump => pump.CreatePump(_services, ct));
        await Task.WhenAll(tasks);
    }
}
