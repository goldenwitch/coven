// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;

namespace Coven.Covenants;

/// <summary>
/// Builder for configuring junction routes that fan-out a single input type to multiple output types.
/// </summary>
/// <typeparam name="TCovenant">The covenant being configured.</typeparam>
/// <typeparam name="TIn">The input entry type being routed.</typeparam>
public interface IJunctionBuilder<TCovenant, TIn>
    where TCovenant : ICovenant
    where TIn : ICovenantEntry<TCovenant>
{
    /// <summary>
    /// Adds a route that transforms matching entries to a single output.
    /// </summary>
    /// <typeparam name="TOut">The output entry type.</typeparam>
    /// <param name="predicate">Predicate that determines if this route applies.</param>
    /// <param name="transform">Transform function for matching entries.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IJunctionBuilder<TCovenant, TIn> Route<TOut>(
        Func<TIn, bool> predicate,
        Func<TIn, TOut> transform)
        where TOut : ICovenantEntry<TCovenant>;

    /// <summary>
    /// Adds a route that transforms matching entries to multiple outputs.
    /// </summary>
    /// <typeparam name="TOut">The output entry type.</typeparam>
    /// <param name="predicate">Predicate that determines if this route applies.</param>
    /// <param name="transform">Transform function that produces multiple outputs for matching entries.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IJunctionBuilder<TCovenant, TIn> RouteMany<TOut>(
        Func<TIn, bool> predicate,
        Func<TIn, IEnumerable<TOut>> transform)
        where TOut : ICovenantEntry<TCovenant>;

    /// <summary>
    /// Adds a fallback route for entries that don't match any predicate.
    /// </summary>
    /// <typeparam name="TOut">The output entry type.</typeparam>
    /// <param name="transform">Transform function for non-matching entries.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IJunctionBuilder<TCovenant, TIn> Fallback<TOut>(
        Func<TIn, TOut> transform)
        where TOut : ICovenantEntry<TCovenant>;
}
