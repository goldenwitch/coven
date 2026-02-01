// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Daemonology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Core.Covenants;

/// <summary>
/// Base class for daemons that host an encapsulated inner sub-graph.
/// Manages inner scriveners, inner daemons, and inner covenant pumps.
/// </summary>
/// <typeparam name="TBoundary">The boundary entry type shared with the outer covenant.</typeparam>
/// <param name="daemonEvents">Scrivener for daemon lifecycle events.</param>
/// <param name="services">The outer service provider.</param>
/// <param name="boundaryScrivener">The boundary scrivener shared with the outer covenant.</param>
/// <param name="manifest">The composite branch manifest describing the inner structure.</param>
public abstract class CompositeDaemon<TBoundary>(
    IScrivener<DaemonEvent> daemonEvents,
    IServiceProvider services,
    IScrivener<TBoundary> boundaryScrivener,
    CompositeBranchManifest manifest) : ContractDaemon(daemonEvents)
    where TBoundary : Entry
{
    private readonly IServiceProvider _outerServices = services;
    private readonly CompositeBranchManifest _manifest = manifest;

    private ServiceProvider? _innerScope;
    private List<IDaemon>? _innerDaemons;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    /// <summary>
    /// The boundary scrivener, shared with the outer covenant.
    /// </summary>
    protected IScrivener<TBoundary> BoundaryScrivener { get; } = boundaryScrivener;

    /// <inheritdoc/>
    public override async Task Start(CancellationToken cancellationToken = default)
    {
        // Build inner service scope with isolated scriveners
        _innerScope = BuildInnerServiceProvider();

        // Collect and start inner daemons
        _innerDaemons = [];
        try
        {
            await StartInnerDaemonsAsync(_innerScope, cancellationToken);
        }
        catch
        {
            // Roll back: shutdown any daemons that started successfully
            await RollbackStartedDaemonsAsync(CancellationToken.None);
            _innerScope.Dispose();
            _innerScope = null;
            _innerDaemons = null;
            throw;
        }

        // Start inner pumps
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pumpTask = RunInnerPumpsAsync(_cts.Token);

        // Transition to Running only after successful startup
        // If transition fails (already running or invalid state), clean up
        if (!await Transition(Status.Running, cancellationToken))
        {
            // Already running - roll back the duplicate startup
            await _cts.CancelAsync();
            await _pumpTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            _cts.Dispose();
            _cts = null;
            _pumpTask = null;
            await RollbackStartedDaemonsAsync(CancellationToken.None);
            _innerScope.Dispose();
            _innerScope = null;
            _innerDaemons = null;
        }
    }

    /// <inheritdoc/>
    public override async Task Shutdown(CancellationToken cancellationToken = default)
    {
        if (!await Transition(Status.Completed, cancellationToken))
        {
            return; // Already completed (idempotent)
        }

        // Cancel pumps
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

        // Shutdown inner daemons in reverse order
        if (_innerDaemons is not null)
        {
            for (int i = _innerDaemons.Count - 1; i >= 0; i--)
            {
                await _innerDaemons[i].Shutdown(cancellationToken);
            }
            _innerDaemons = null;
        }

        // Dispose child scope
        _innerScope?.Dispose();
        _innerScope = null;
    }

    private ServiceProvider BuildInnerServiceProvider()
    {
        ServiceCollection innerServices = new();

        // Forward outer services that inner daemons might need
        ForwardOuterService<ILoggerFactory>(innerServices);

        // Register boundary scrivener (shared with outer covenant)
        innerServices.AddSingleton(BoundaryScrivener);

        // Register InMemoryScrivener for each inner manifest's journal entry type
        // (Boundary scrivener is already registered above — it's not in InnerManifests)
        foreach (BranchManifest innerManifest in _manifest.InnerManifests)
        {
            Type entryType = innerManifest.JournalEntryType;
            Type scrivenerInterface = typeof(IScrivener<>).MakeGenericType(entryType);
            Type scrivenerImpl = typeof(InMemoryScrivener<>).MakeGenericType(entryType);
            innerServices.AddSingleton(scrivenerInterface, scrivenerImpl);
        }

        return innerServices.BuildServiceProvider();
    }

    private void ForwardOuterService<TService>(IServiceCollection innerServices) where TService : class
    {
        TService? service = _outerServices.GetService<TService>();
        if (service is not null)
        {
            innerServices.AddSingleton(service);
        }
    }

    private async Task StartInnerDaemonsAsync(IServiceProvider innerScope, CancellationToken cancellationToken)
    {
        foreach (BranchManifest innerManifest in _manifest.InnerManifests)
        {
            foreach (Type daemonType in innerManifest.RequiredDaemons)
            {
                IDaemon daemon = (IDaemon)ActivatorUtilities.CreateInstance(innerScope, daemonType);
                _innerDaemons!.Add(daemon);
                await daemon.Start(cancellationToken);
            }
        }
    }

    private async Task RollbackStartedDaemonsAsync(CancellationToken cancellationToken)
    {
        if (_innerDaemons is null)
        {
            return;
        }

        // Shutdown in reverse order
        for (int i = _innerDaemons.Count - 1; i >= 0; i--)
        {
            try
            {
                await _innerDaemons[i].Shutdown(cancellationToken);
            }
            catch
            {
                // Best-effort cleanup; continue shutting down remaining daemons
            }
        }
    }

    private async Task RunInnerPumpsAsync(CancellationToken ct)
    {
        IEnumerable<Task> tasks = _manifest.InnerPumps.Select(pump => pump.CreatePump(_innerScope!, ct));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Releases resources held by the composite daemon.
    /// Callers should prefer <see cref="Shutdown"/> for graceful cleanup.
    /// </summary>
    public new void Dispose()
    {
        // Cancel and clean up pumps
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        // Best-effort daemon cleanup (can't await in Dispose)
        if (_innerDaemons is not null)
        {
            foreach (IDaemon daemon in _innerDaemons)
            {
                if (daemon is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _innerDaemons = null;
        }

        // Dispose inner scope
        _innerScope?.Dispose();
        _innerScope = null;

        // Call base dispose
        base.Dispose();
    }
}
