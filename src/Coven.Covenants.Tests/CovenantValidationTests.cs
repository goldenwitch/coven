// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Covenants.Tests;

/// <summary>
/// Tests for covenant validation rules.
/// </summary>
public class CovenantValidationTests
{
    // Sample base journal entry type for testing
    private abstract record TestJournalEntry : Entry;

    // Sample entry types for testing
    private sealed record SourceEntry : TestJournalEntry;

    private sealed record TargetEntry : TestJournalEntry;

    private sealed record UnroutedEntry : TestJournalEntry;

    private sealed record ConsumedEntry : TestJournalEntry;

    [Fact]
    public void CovenantWithAllRoutesAndTerminalsSucceeds()
    {
        // Arrange
        ServiceCollection services = new();
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry), typeof(UnroutedEntry) },
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        // Act & Assert - should not throw
        services.BuildCoven(coven =>
        {
            coven.Covenant()
                .Connect(branch)
                .Routes(c =>
                {
                    c.Route<SourceEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry()));
                    c.Terminal<UnroutedEntry>();
                });
        });
    }

    [Fact]
    public void CovenantWithMissingRouteThrowsValidationException()
    {
        // Arrange
        ServiceCollection services = new();
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry), typeof(UnroutedEntry) },
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        // Act & Assert
        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch)
                    .Routes(c =>
                    {
                        // Only route SourceEntry, missing UnroutedEntry
                        c.Route<SourceEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry()));
                    });
            });
        });

        Assert.Contains("UnroutedEntry", exception.Message);
        Assert.Contains("is produced but has no route and is not terminal", exception.Message);
    }

    [Fact]
    public void CovenantWithMissingConsumerRouteThrowsValidationException()
    {
        // Arrange
        ServiceCollection services = new();
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry) },
            Consumes: new HashSet<Type> { typeof(ConsumedEntry) },
            RequiredDaemons: []);

        // Act & Assert
        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch)
                    .Routes(c =>
                    {
                        // Route to TargetEntry, but branch consumes ConsumedEntry
                        c.Route<SourceEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry()));
                    });
            });
        });

        Assert.Contains("ConsumedEntry", exception.Message);
        Assert.Contains("is consumed but nothing routes to it", exception.Message);
    }

    [Fact]
    public void CovenantWithBothRouteAndTerminalThrowsValidationException()
    {
        // Arrange
        ServiceCollection services = new();
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry) },
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        // Act & Assert
        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch)
                    .Routes(c =>
                    {
                        c.Route<SourceEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry()));
                        c.Terminal<SourceEntry>(); // Also terminal - invalid
                    });
            });
        });

        Assert.Contains("SourceEntry", exception.Message);
        Assert.Contains("has both a Route and a Terminal", exception.Message);
    }

    [Fact]
    public void CovenantWithMultipleRoutesForSameSourceThrowsValidationException()
    {
        // Arrange
        ServiceCollection services = new();
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry) },
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        // Act & Assert
        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch)
                    .Routes(c =>
                    {
                        c.Route<SourceEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry()));
                        c.Route<SourceEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry())); // Duplicate
                    });
            });
        });

        Assert.Contains("SourceEntry", exception.Message);
        Assert.Contains("has multiple routes", exception.Message);
    }

    [Fact]
    public void CovenantWithMultipleManifestsValidatesAcrossBranches()
    {
        // Arrange
        ServiceCollection services = new();
        BranchManifest branch1 = new(
            "Branch1",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry) },
            Consumes: new HashSet<Type>(),
            RequiredDaemons: []);

        BranchManifest branch2 = new(
            "Branch2",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type>(),
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        // Act & Assert - should succeed with proper routing across branches
        services.BuildCoven(coven =>
        {
            coven.Covenant()
                .Connect(branch1)
                .Connect(branch2)
                .Routes(c =>
                {
                    c.Route<SourceEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry()));
                });
        });
    }
}
