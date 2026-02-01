// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Daemonology;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class CompositeDaemonTests
{
    #region Test Entry Types

    public abstract record TestBoundaryEntry : Entry;
    public record TestBoundaryInput(string Value) : TestBoundaryEntry;
    public record TestBoundaryOutput(string Value) : TestBoundaryEntry;

    public abstract record TestInnerEntry : Entry;
    public record TestInnerInput(string Value) : TestInnerEntry;
    public record TestInnerOutput(string Value) : TestInnerEntry;

    #endregion

    #region Test Daemons

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
    /// </summary>
#pragma warning disable CS9113 // Parameter 'scrivener' is unread - required for DI resolution
    public class FailingInnerDaemon(IScrivener<TestInnerEntry> scrivener) : IDaemon
#pragma warning restore CS9113
    {
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

    #endregion

    #region Test Composite Daemon

    public class TestCompositeDaemon(
        IScrivener<DaemonEvent> daemonEvents,
        IServiceProvider services,
        IScrivener<TestBoundaryEntry> boundaryScrivener,
        CompositeBranchManifest manifest)
        : CompositeDaemon<TestBoundaryEntry>(daemonEvents, services, boundaryScrivener, manifest);

    #endregion

    #region Test Infrastructure

    private static (CompositeBranchManifest manifest, IScrivener<TestBoundaryEntry> boundary) CreateTestManifest(
        Type? daemonType = null)
    {
        daemonType ??= typeof(TestInnerDaemon);

        HashSet<Type> produces = [typeof(TestBoundaryOutput)];
        HashSet<Type> consumes = [typeof(TestBoundaryInput)];

        InnerCovenantBuilder builder = new(typeof(TestBoundaryEntry), produces, consumes);

        BranchManifest inner = builder.Branch(
            "Inner",
            typeof(TestInnerEntry),
            produces: new HashSet<Type> { typeof(TestInnerOutput) },
            consumes: new HashSet<Type> { typeof(TestInnerInput) },
            daemons: [daemonType]);

        builder.ConnectBoundary();
        builder.Connect(inner);

        builder.Routes(c =>
        {
            c.Route<TestBoundaryInput, TestInnerInput>(
                (e, _) => Task.FromResult(new TestInnerInput(e.Value)));
            c.Route<TestInnerOutput, TestBoundaryOutput>(
                (e, _) => Task.FromResult(new TestBoundaryOutput(e.Value)));
        });

        CompositeBranchManifest manifest = new(
            "Test",
            typeof(TestBoundaryEntry),
            produces,
            consumes,
            builder.InnerManifests,
            builder.InnerPumps,
            typeof(TestCompositeDaemon));

        IScrivener<TestBoundaryEntry> boundary = new InMemoryScrivener<TestBoundaryEntry>();

        return (manifest, boundary);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        ServiceCollection services = new();
        return services.BuildServiceProvider();
    }

    private static InMemoryScrivener<DaemonEvent> CreateDaemonEventsScrivener()
    {
        return new InMemoryScrivener<DaemonEvent>();
    }

    #endregion

    #region Tests

    [Fact]
    public async Task CompositeDaemonStartsAndTransitionsToRunning()
    {
        // Arrange
        (CompositeBranchManifest manifest, IScrivener<TestBoundaryEntry> boundary) = CreateTestManifest();
        ServiceProvider services = CreateServiceProvider();
        IScrivener<DaemonEvent> daemonEvents = CreateDaemonEventsScrivener();
        TestCompositeDaemon daemon = new(daemonEvents, services, boundary, manifest);

        // Act
        await daemon.Start();

        try
        {
            // Assert
            Assert.Equal(Status.Running, daemon.Status);
        }
        finally
        {
            await daemon.Shutdown();
        }
    }

    [Fact]
    public async Task CompositeDaemonShutdownTransitionsToCompleted()
    {
        // Arrange
        (CompositeBranchManifest manifest, IScrivener<TestBoundaryEntry> boundary) = CreateTestManifest();
        ServiceProvider services = CreateServiceProvider();
        IScrivener<DaemonEvent> daemonEvents = CreateDaemonEventsScrivener();
        TestCompositeDaemon daemon = new(daemonEvents, services, boundary, manifest);

        await daemon.Start();

        // Act
        await daemon.Shutdown();

        // Assert
        Assert.Equal(Status.Completed, daemon.Status);
    }

    [Fact]
    public async Task CompositeDaemonRoutesEntryThroughInnerBranch()
    {
        // Arrange
        (CompositeBranchManifest manifest, IScrivener<TestBoundaryEntry> boundary) = CreateTestManifest();
        ServiceProvider services = CreateServiceProvider();
        IScrivener<DaemonEvent> daemonEvents = CreateDaemonEventsScrivener();
        TestCompositeDaemon daemon = new(daemonEvents, services, boundary, manifest);

        await daemon.Start();

        try
        {
            // Act: Write input to boundary scrivener
            await boundary.WriteAsync(new TestBoundaryInput("Hello"));

            // Assert: Wait for output to appear in boundary scrivener
            // Use a timeout to prevent test hanging
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
            TestBoundaryOutput? output = null;

            await foreach ((long _, TestBoundaryEntry entry) in boundary.TailAsync(0, cts.Token))
            {
                if (entry is TestBoundaryOutput boundaryOutput)
                {
                    output = boundaryOutput;
                    break;
                }
            }

            Assert.NotNull(output);
            Assert.Equal("Processed: Hello", output.Value);
        }
        finally
        {
            await daemon.Shutdown();
        }
    }

    [Fact]
    public async Task CompositeDaemonFailsIfInnerDaemonFailsToStart()
    {
        // Arrange
        (CompositeBranchManifest manifest, IScrivener<TestBoundaryEntry> boundary) =
            CreateTestManifest(daemonType: typeof(FailingInnerDaemon));
        ServiceProvider services = CreateServiceProvider();
        IScrivener<DaemonEvent> daemonEvents = CreateDaemonEventsScrivener();
        TestCompositeDaemon daemon = new(daemonEvents, services, boundary, manifest);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => daemon.Start());

        // Daemon should remain in Stopped state or transition to Completed after rollback
        Assert.True(
            daemon.Status is Status.Stopped or Status.Completed,
            $"Expected Stopped or Completed, but was {daemon.Status}");
    }

    #endregion
}
