// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Daemonology;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class ScopeLifecycleTests
{
    private sealed class FailureSignal
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class BackgroundFailureDaemon(
        IScrivener<DaemonEvent> scrivener,
        FailureSignal signal) : ContractDaemon(scrivener)
    {
        private readonly FailureSignal _signal = signal;
        private Task? _failureTask;

        public override async Task Start(CancellationToken cancellationToken = default)
        {
            await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
            _failureTask = Task.Run(async () =>
            {
                await _signal.Started.Task.ConfigureAwait(false);
                await Fail(new InvalidOperationException("daemon failed"), CancellationToken.None).ConfigureAwait(false);
            }, CancellationToken.None);
        }

        public override async Task Shutdown(CancellationToken cancellationToken = default)
        {
            await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
            if (_failureTask is not null)
            {
                await _failureTask.ConfigureAwait(false);
            }
        }
    }

    private sealed class SlowEcho(FailureSignal signal) : IMagikBlock<string, string>
    {
        private readonly FailureSignal _signal = signal;

        public async Task<string> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            _signal.Started.TrySetResult();
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
            return input;
        }
    }

    private sealed class Tracker : IDisposable
    {
        public static int Created;
        public static int Disposed;
        public int Id { get; }
        public Tracker() { Id = Interlocked.Increment(ref Created); }
        public void Dispose() => Interlocked.Increment(ref Disposed);
        public static void Reset() { Created = 0; Disposed = 0; }
    }

    private sealed class UsesTracker(Tracker tracker) : IMagikBlock<string, string>
    {
        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult($"{input}:{tracker.Id}");
    }

    [Fact]
    public async Task ScopedDependencyIsNewPerRitualAndDisposedAfter()
    {
        Tracker.Reset();

        using TestHost host = TestBed.BuildPush(
            build: c =>
            {
                _ = c.MagikBlock<string, string, UsesTracker>(ServiceLifetime.Transient)
                    .Done();
            },
            configureServices: s => s.AddScoped<Tracker>()
        );

        string r1 = await host.Coven.Ritual<string, string>("x");
        string r2 = await host.Coven.Ritual<string, string>("y");

        // Each ritual should get a new scoped Tracker
        Assert.NotEqual(r1.Split(':')[1], r2.Split(':')[1]);

        // All created scoped services should be disposed when their ritual scope ends
        Assert.Equal(Tracker.Created, Tracker.Disposed);
        Assert.Equal(2, Tracker.Created);
    }

    private interface IMissing { }

    private sealed class NeedsMissing(IMissing missing) : IMagikBlock<string, int>
    {
        private readonly IMissing _missing = missing;

        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            _ = _missing is null; // read to satisfy analyzers; resolution fails earlier
            return Task.FromResult(input.Length);
        }
    }

    [Fact]
    public async Task MissingDependencyThrowsClearError()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, NeedsMissing>()
                .Done();
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.Coven.Ritual<string, int>("abc"));
        Assert.Contains("Unable to resolve", ex.Message);
    }

    [Fact]
    public async Task BackgroundDaemonFailureAbortsRitual()
    {
        FailureSignal signal = new();

        using TestHost host = TestBed.BuildPush(
            build: c =>
            {
                _ = c.MagikBlock<string, string, SlowEcho>(ServiceLifetime.Transient)
                    .Done();
            },
            configureServices: s =>
            {
                s.AddSingleton(signal);
                s.AddScoped<IScrivener<DaemonEvent>>(_ => new InMemoryScrivener<DaemonEvent>());
                s.AddScoped<IDaemon, BackgroundFailureDaemon>();
            });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await host.Coven.Ritual<string, string>("hello"));

        Assert.Equal("daemon failed", ex.Message);
    }

    private sealed class DisposableBlock : IMagikBlock<string, string>, IDisposable
    {
        public static int Created;
        public static int Disposed;
        public DisposableBlock() { Interlocked.Increment(ref Created); }
        public void Dispose() { Interlocked.Increment(ref Disposed); }
        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input);
        public static void Reset() { Created = 0; Disposed = 0; }
    }

    [Fact]
    public async Task TransientBlockInstanceDisposedWhenScopeEnds()
    {
        DisposableBlock.Reset();
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, string, DisposableBlock>(ServiceLifetime.Transient)
                .Done();
        });

        await host.Coven.Ritual<string, string>("z");

        Assert.Equal(1, DisposableBlock.Created);
        Assert.Equal(1, DisposableBlock.Disposed);
    }

    private sealed class SingletonTracker : IDisposable
    {
        public static int Created;
        public static int Disposed;
        public int Id { get; }
        public SingletonTracker() { Id = Interlocked.Increment(ref Created); }
        public void Dispose() { Interlocked.Increment(ref Disposed); }
        public static void Reset() { Created = 0; Disposed = 0; }
    }

    private sealed class UsesSingleton(SingletonTracker tracker) : IMagikBlock<string, string>
    {
        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult($"{input}:{tracker.Id}");
    }

    [Fact]
    public async Task SingletonDependencyIsReusedAcrossRitualsAndDisposedWhenProviderDisposes()
    {
        SingletonTracker.Reset();

        string id1, id2;
        using (TestHost host = TestBed.BuildPush(
            build: c =>
            {
                _ = c.MagikBlock<string, string, UsesSingleton>(ServiceLifetime.Transient)
                    .Done();
            },
            configureServices: s => s.AddSingleton<SingletonTracker>()))
        {
            string r1 = await host.Coven.Ritual<string, string>("one");
            string r2 = await host.Coven.Ritual<string, string>("two");
            id1 = r1.Split(':')[1];
            id2 = r2.Split(':')[1];
            Assert.Equal(id1, id2); // same singleton instance id across rituals
            Assert.Equal(1, SingletonTracker.Created);
            Assert.Equal(0, SingletonTracker.Disposed); // provider not yet disposed
        }

        // After provider disposal, singleton should be disposed
        Assert.Equal(1, SingletonTracker.Disposed);
    }
}
