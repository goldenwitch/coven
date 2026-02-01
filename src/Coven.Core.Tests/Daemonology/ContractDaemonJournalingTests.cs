// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Daemonology.Tests;

public class ContractDaemonJournalingTests
{
    [Fact]
    public async Task StartWritesStatusChangeEvent()
    {
        // Intent: State transitions are journaled for observability.
        // External systems can tail the journal to react to daemon lifecycle.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();

        List<DaemonEvent> events = scrivener.ReadAll();
        Assert.Single(events);
    }

    [Fact]
    public async Task ShutdownWritesStatusChangeEvent()
    {
        // Intent: Shutdown is journaled so observers know the daemon completed.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Shutdown();

        List<DaemonEvent> events = scrivener.ReadAll();
        Assert.Equal(2, events.Count); // Start + Shutdown
    }

    [Fact]
    public async Task IdempotentStartDoesNotWriteDuplicateEvent()
    {
        // Intent: Idempotent calls should not pollute the journal.
        // Only actual state transitions produce events.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Start(); // Idempotent
        await daemon.Start(); // Idempotent

        List<DaemonEvent> events = scrivener.ReadAll();
        Assert.Single(events); // Only one StatusChanged(Running)
    }
}
