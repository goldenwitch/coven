// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Core.Covenants;

/// <summary>
/// Fluent API for defining routes within a covenant.
/// Each entry type must have exactly one disposition: a Route or a Terminal.
/// </summary>
public interface ICovenant
{
    /// <summary>
    /// Defines a route from <typeparamref name="TSource"/> to <typeparamref name="TTarget"/> using an async lambda.
    /// </summary>
    /// <typeparam name="TSource">The entry type being routed from.</typeparam>
    /// <typeparam name="TTarget">The entry type being routed to.</typeparam>
    /// <param name="map">Async transformation function.</param>
    /// <returns>The same covenant for fluent chaining.</returns>
    ICovenant Route<TSource, TTarget>(Func<TSource, CancellationToken, Task<TTarget>> map)
        where TSource : Entry
        where TTarget : Entry;

    /// <summary>
    /// Defines a route from <typeparamref name="TSource"/> to <typeparamref name="TTarget"/> using a DI-resolved transmuter.
    /// </summary>
    /// <typeparam name="TSource">The entry type being routed from.</typeparam>
    /// <typeparam name="TTarget">The entry type being routed to.</typeparam>
    /// <typeparam name="TTransmuter">The transmuter type resolved from DI.</typeparam>
    /// <returns>The same covenant for fluent chaining.</returns>
    ICovenant Route<TSource, TTarget, TTransmuter>()
        where TSource : Entry
        where TTarget : Entry
        where TTransmuter : class, ITransmuter<TSource, TTarget>;

    /// <summary>
    /// Marks an entry type as terminal—explicitly not routed anywhere.
    /// Required to satisfy completeness validation; no implicit ignoring allowed.
    /// </summary>
    /// <typeparam name="TEntry">The entry type that terminates here.</typeparam>
    /// <returns>The same covenant for fluent chaining.</returns>
    ICovenant Terminal<TEntry>()
        where TEntry : Entry;
}
