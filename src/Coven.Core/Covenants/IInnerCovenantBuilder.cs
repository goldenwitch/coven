// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Fluent API for building inner covenants within a composite branch.
/// Allows declaring inner branches, connecting them for routing, and defining internal routes.
/// </summary>
public interface IInnerCovenantBuilder
{
    /// <summary>
    /// Declares an inner branch within the composite.
    /// </summary>
    /// <param name="name">Human-readable branch identifier.</param>
    /// <param name="journalEntryType">The base entry type for this branch's journal.</param>
    /// <param name="produces">Entry types this branch writes to its journal.</param>
    /// <param name="consumes">Entry types this branch reads from its journal.</param>
    /// <param name="daemons">Daemon types required for this branch to function.</param>
    /// <returns>The manifest for the declared branch.</returns>
    BranchManifest Branch(
        string name,
        Type journalEntryType,
        IReadOnlySet<Type> produces,
        IReadOnlySet<Type> consumes,
        IReadOnlyList<Type> daemons);

    /// <summary>
    /// Connects an inner branch manifest for routing within the composite.
    /// </summary>
    /// <param name="manifest">The branch manifest to connect.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IInnerCovenantBuilder Connect(BranchManifest manifest);

    /// <summary>
    /// Connects the boundary journal for routing.
    /// The boundary journal is implicitly shared with the outer covenant.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    IInnerCovenantBuilder ConnectBoundary();

    /// <summary>
    /// Defines routes for the inner covenant using the standard covenant routing API.
    /// </summary>
    /// <param name="configure">Action to configure routes via <see cref="ICovenant"/>.</param>
    void Routes(Action<ICovenant> configure);
}
