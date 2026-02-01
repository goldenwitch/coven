// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class EntryJournalUniquenessTests
{
    [Fact]
    public void EntryTypeInMultipleJournalsThrowsValidationException()
    {
        // Arrange: Two manifests with different JournalEntryTypes but overlapping entry types
        ServiceCollection services = new();

        // Branch1 uses Journal1Entry as its journal, produces SharedEntry
        BranchManifest branch1 = new(
            Name: "Branch1",
            JournalEntryType: typeof(Journal1Entry),
            Produces: new HashSet<Type> { typeof(SharedEntry) },
            Consumes: new HashSet<Type>(),
            RequiredDaemons: []);

        // Branch2 uses Journal2Entry as its journal, also produces SharedEntry (conflict!)
        BranchManifest branch2 = new(
            Name: "Branch2",
            JournalEntryType: typeof(Journal2Entry),
            Produces: new HashSet<Type> { typeof(SharedEntry) },
            Consumes: new HashSet<Type>(),
            RequiredDaemons: []);

        // Act & Assert
        CovenantValidationException ex = Assert.Throws<CovenantValidationException>(() =>
        {
            services.BuildCoven(coven =>
            {
                coven.Covenant()
                    .Connect(branch1)
                    .Connect(branch2)
                    .Routes(c =>
                    {
                        c.Terminal<SharedEntry>();
                    });
            });
        });

        // Verify error message contains expected content
        Assert.Contains("SharedEntry", ex.Message);
        Assert.Contains("appears in multiple journals", ex.Message);
        Assert.Contains("Branch1", ex.Message);
        Assert.Contains("Branch2", ex.Message);
    }

    [Fact]
    public void EntryTypeInSameJournalSucceeds()
    {
        // Arrange: Two manifests with the same JournalEntryType and overlapping entry types (OK)
        ServiceCollection services = new();

        // Both branches use the same journal type - this is fine
        // Both produce the same entry type but with same journal = no conflict
        BranchManifest branch1 = new(
            Name: "Branch1",
            JournalEntryType: typeof(SharedJournalEntry),
            Produces: new HashSet<Type> { typeof(OutputEntry) },
            Consumes: new HashSet<Type>(),
            RequiredDaemons: []);

        BranchManifest branch2 = new(
            Name: "Branch2",
            JournalEntryType: typeof(SharedJournalEntry),
            Produces: new HashSet<Type> { typeof(OutputEntry) },
            Consumes: new HashSet<Type>(),
            RequiredDaemons: []);

        // Act - should not throw multi-journal error (same journal type is OK)
        services.BuildCoven(coven =>
        {
            coven.Covenant()
                .Connect(branch1)
                .Connect(branch2)
                .Routes(c =>
                {
                    c.Terminal<OutputEntry>();
                });
        });

        // No exception = success (specifically: no "appears in multiple journals" error)
    }

    // Test entry types
    private sealed record Journal1Entry : Entry;
    private sealed record Journal2Entry : Entry;
    private sealed record SharedJournalEntry : Entry;
    private sealed record SharedEntry : Entry;
    private sealed record OutputEntry : Entry;
}
