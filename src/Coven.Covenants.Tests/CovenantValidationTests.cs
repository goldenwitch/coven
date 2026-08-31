// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Transmutation;
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

    private sealed record ConditionalSourceEntry(int Choice) : TestJournalEntry;

    private sealed record TargetEntry : TestJournalEntry;

    private sealed record AlternateTargetEntry : TestJournalEntry;

    private sealed record UnroutedEntry : TestJournalEntry;

    private sealed record ConsumedEntry : TestJournalEntry;

    private sealed class RouteTransmuter : ITransmuter<SourceEntry, TargetEntry>
    {
        public Task<TargetEntry> Transmute(SourceEntry Input, CancellationToken cancellationToken = default)
            => Task.FromResult(new TargetEntry());
    }

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

    [Fact]
    public void CovenantWithMultipleFilteredRoutesForSameSourceSucceeds()
    {
        ServiceCollection services = new();
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(ConditionalSourceEntry) },
            Consumes: new HashSet<Type> { typeof(TargetEntry), typeof(AlternateTargetEntry) },
            RequiredDaemons: []);

        services.BuildCoven(coven =>
        {
            coven.Covenant()
                .Connect(branch)
                .Routes(c =>
                {
                    c.Route<ConditionalSourceEntry, TargetEntry>(entry => entry.Choice == 0, (entry, ct) => Task.FromResult(new TargetEntry()));
                    c.Route<ConditionalSourceEntry, AlternateTargetEntry>(entry => entry.Choice != 0, (entry, ct) => Task.FromResult(new AlternateTargetEntry()));
                });
        });
    }

    /// <summary>
    /// A route whose target no connected branch declares is rejected with an actionable
    /// message.
    /// </summary>
    /// <remarks>
    /// Regression coverage: pump construction resolves each endpoint to a journal through the
    /// manifests, so an undeclared target previously escaped validation and surfaced at ritual
    /// start as a bare <see cref="KeyNotFoundException"/> naming the type but nothing else. It
    /// is reachable from ordinary use — a branch that declares a type only under an optional
    /// registration flag, routed to unconditionally.
    /// </remarks>
    [Fact]
    public void CovenantWithUndeclaredRouteTargetThrowsValidationException()
    {
        ServiceCollection services = new();

        // Consumes nothing, so the undeclared target is the only defect present.
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry) },
            Consumes: new HashSet<Type>(),
            RequiredDaemons: []);

        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch)
                    .Routes(c =>
                    {
                        // AlternateTargetEntry appears in no manifest.
                        c.Route<SourceEntry, AlternateTargetEntry>(
                            (e, ct) => Task.FromResult(new AlternateTargetEntry()));
                    });
            });
        });

        Assert.Contains("1 error", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AlternateTargetEntry), exception.Message, StringComparison.Ordinal);
        Assert.Contains("no connected branch declares it", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same guarantee for the source side: routing from a type nothing declares is a
    /// disconnected branch, not a silent no-op.
    /// </summary>
    [Fact]
    public void CovenantWithUndeclaredRouteSourceThrowsValidationException()
    {
        ServiceCollection services = new();

        // Produces nothing, so the undeclared source is the only defect present.
        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type>(),
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch)
                    .Routes(c =>
                    {
                        // UnroutedEntry is produced by no connected branch.
                        c.Route<UnroutedEntry, TargetEntry>((e, ct) => Task.FromResult(new TargetEntry()));
                    });
            });
        });

        Assert.Contains("1 error", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(UnroutedEntry), exception.Message, StringComparison.Ordinal);
        Assert.Contains("no connected branch declares it", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TransmuterRouteRequiresConcreteRegistration()
    {
        ServiceCollection services = new();
        services.AddTransient<ITransmuter<SourceEntry, TargetEntry>, RouteTransmuter>();

        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry) },
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        CovenantValidationException exception = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch)
                    .Routes(c => c.Route<SourceEntry, TargetEntry, RouteTransmuter>());
            });
        });

        Assert.Contains(nameof(RouteTransmuter), exception.Message);
        Assert.Contains("service container", exception.Message);
    }

    [Fact]
    public void TransmuterRouteSucceedsWithConcreteRegistration()
    {
        ServiceCollection services = new();
        services.AddTransient<RouteTransmuter>();

        BranchManifest branch = new(
            "TestBranch",
            JournalEntryType: typeof(TestJournalEntry),
            Produces: new HashSet<Type> { typeof(SourceEntry) },
            Consumes: new HashSet<Type> { typeof(TargetEntry) },
            RequiredDaemons: []);

        services.BuildCoven(coven =>
        {
            coven.Covenant()
                .Connect(branch)
                .Routes(c => c.Route<SourceEntry, TargetEntry, RouteTransmuter>());
        });
    }
}
