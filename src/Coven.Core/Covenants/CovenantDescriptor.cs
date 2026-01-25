// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Immutable descriptor of a validated covenant, stored in DI for runtime access.
/// Contains pre-built pumps for executing routes at runtime.
/// </summary>
internal sealed record CovenantDescriptor(
    IReadOnlyList<BranchManifest> Manifests,
    IReadOnlyList<PumpDescriptor> Pumps);
