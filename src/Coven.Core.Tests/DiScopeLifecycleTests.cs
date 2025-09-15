// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Di;
using Xunit;

namespace Coven.Core.Tests;

public class DiScopeLifecycleTests
{
    private sealed class Tracker : IDisposable
    {
        public static int Created;
        public static int Disposed;
        public int Id { get; }
        public Tracker() { Id = Interlocked.Increment(ref Created); }
        public void Dispose() => Interlocked.Increment(ref Disposed);
        public static void Reset() { Created = 0; Disposed = 0; }
    }

    private sealed class UsesTracker : IMagikBlock<string, string>
    {
        private readonly Tracker tracker;
        public UsesTracker(Tracker tracker) { this.tracker = tracker; }
        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult($"{input}:{tracker.Id}");
    }

    [Fact]
    public async Task Scoped_Dependency_Is_New_Per_Ritual_And_Disposed_After()
    {
        Tracker.Reset();

        var services = new ServiceCollection();
        services.AddScoped<Tracker>();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, string, UsesTracker>(ServiceLifetime.Transient);
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var r1 = await coven.Ritual<string, string>("x");
        var r2 = await coven.Ritual<string, string>("y");

        // Each ritual should get a new scoped Tracker
        Assert.NotEqual(r1.Split(':')[1], r2.Split(':')[1]);

        // All created scoped services should be disposed when their ritual scope ends
        Assert.Equal(Tracker.Created, Tracker.Disposed);
        Assert.Equal(2, Tracker.Created);
    }

    private interface IMissing { }

    private sealed class NeedsMissing : IMagikBlock<string, int>
    {
        public NeedsMissing(IMissing missing) { }
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
    }

    [Fact]
    public async Task Missing_Dependency_Throws_Clear_Error()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, NeedsMissing>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await coven.Ritual<string, int>("abc"));
        Assert.Contains("Unable to resolve", ex.Message);
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
    public async Task Transient_Block_Instance_Disposed_When_Scope_Ends()
    {
        DisposableBlock.Reset();
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, string, DisposableBlock>(ServiceLifetime.Transient);
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var _ = await coven.Ritual<string, string>("z");

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

    private sealed class UsesSingleton : IMagikBlock<string, string>
    {
        private readonly SingletonTracker tracker;
        public UsesSingleton(SingletonTracker tracker) { this.tracker = tracker; }
        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult($"{input}:{tracker.Id}");
    }

    [Fact]
    public async Task Singleton_Dependency_Is_Reused_Across_Rituals_And_Disposed_When_Provider_Disposes()
    {
        SingletonTracker.Reset();

        var services = new ServiceCollection();
        services.AddSingleton<SingletonTracker>();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, string, UsesSingleton>(ServiceLifetime.Transient);
            c.Done();
        });

        string id1, id2;
        using (var sp = services.BuildServiceProvider())
        {
            var coven = sp.GetRequiredService<ICoven>();
            var r1 = await coven.Ritual<string, string>("one");
            var r2 = await coven.Ritual<string, string>("two");
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
