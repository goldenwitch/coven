// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Builder for connecting branch manifests and defining routes between them.
/// Collects all manifests and route definitions, then validates at build time.
/// </summary>
public interface ICovenantBuilder
{
    /// <summary>
    /// Connects a branch manifest to this covenant.
    /// The manifest's Produces and Consumes types become part of the covenant's validation scope.
    /// </summary>
    /// <param name="manifest">The branch manifest to connect.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    ICovenantBuilder Connect(BranchManifest manifest);

    /// <summary>
    /// Defines routes within the covenant. Called after all manifests are connected.
    /// Every type in any connected manifest's Produces must have a Route or Terminal.
    /// Every type in any connected manifest's Consumes must have a Route producing it.
    /// </summary>
    /// <param name="configure">Callback to define routes using <see cref="ICovenant"/>.</param>
    void Routes(Action<ICovenant> configure);
}
