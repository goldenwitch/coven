// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Declares a composite branch that encapsulates inner branches with their own journals and routing.
/// The composite exposes a boundary journal type to the outer covenant while managing internal complexity.
/// </summary>
/// <param name="Name">Human-readable composite identifier (e.g., "Spellcasting").</param>
/// <param name="BoundaryJournalType">The entry type visible at the boundary (e.g., typeof(SpellEntry)).</param>
/// <param name="Produces">Entry types this composite writes to the boundary journal (outputs visible to outer covenant).</param>
/// <param name="Consumes">Entry types this composite reads from the boundary journal (inputs from outer covenant).</param>
/// <param name="InnerManifests">The branch manifests for internal branches within this composite.</param>
/// <param name="InnerPumps">Pre-built pump descriptors for routing within the inner covenant.</param>
/// <param name="CompositeDaemonType">The daemon type that hosts and orchestrates this composite's inner structure.</param>
public sealed record CompositeBranchManifest(
    string Name,
    Type BoundaryJournalType,
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<BranchManifest> InnerManifests,
    IReadOnlyList<PumpDescriptor> InnerPumps,
    Type CompositeDaemonType);
