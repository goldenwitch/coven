// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Daemonology;

namespace Coven.Core.Tests.Infrastructure;

/// <summary>
/// Test entry types for composite daemon testing.
/// </summary>
public abstract record TestBoundaryEntry : Entry;
public record TestBoundaryInput(string Value) : TestBoundaryEntry;
public record TestBoundaryOutput(string Value) : TestBoundaryEntry;

public abstract record TestInnerEntry : Entry;
public record TestInnerInput(string Value) : TestInnerEntry;
public record TestInnerOutput(string Value) : TestInnerEntry;

/// <summary>
/// Inner daemon that transforms TestInnerInput → TestInnerOutput.
/// </summary>
public class TestInnerDaemon(IScrivener<TestInnerEntry> scrivener) : IDaemon
{
    private readonly IScrivener<TestInnerEntry> _scrivener = scrivener;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public Status Status { get; private set; } = Status.Stopped;

    public Task Start(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = RunAsync(_cts.Token);
        Status = Status.Running;
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await foreach ((long _, TestInnerEntry entry) in _scrivener.TailAsync(0, ct))
        {
            if (entry is TestInnerInput input)
            {
                await _scrivener.WriteAsync(new TestInnerOutput($"Processed: {input.Value}"), ct);
            }
        }
    }

    public async Task Shutdown(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            if (_runTask != null)
            {
                await _runTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            _cts.Dispose();
        }

        Status = Status.Completed;
    }
}

/// <summary>
/// Inner daemon that throws on Start for failure testing.
/// The scrivener parameter is required for DI activation via ActivatorUtilities.CreateInstance,
/// which injects IScrivener&lt;TestInnerEntry&gt; from the inner service scope.
/// </summary>
public class FailingInnerDaemon : IDaemon
{
    public FailingInnerDaemon(IScrivener<TestInnerEntry> scrivener)
    {
        _ = scrivener; // Required for DI activation; daemon fails before using it
    }

    public Status Status { get; private set; } = Status.Stopped;

    public Task Start(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Intentional failure on start");
    }

    public Task Shutdown(CancellationToken cancellationToken = default)
    {
        Status = Status.Completed;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test composite daemon for verifying CompositeDaemon behavior.
/// </summary>
public class TestCompositeDaemon(
    IScrivener<DaemonEvent> daemonEvents,
    IServiceProvider services,
    IScrivener<TestBoundaryEntry> boundaryScrivener,
    CompositeBranchManifest manifest)
    : CompositeDaemon<TestBoundaryEntry>(daemonEvents, services, boundaryScrivener, manifest);
