// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Daemonology;
using Coven.Core.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class CompositeDaemonTests
{
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
}
