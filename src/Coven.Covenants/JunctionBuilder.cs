// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;

namespace Coven.Covenants;

/// <summary>
/// Builder implementation that collects junction route definitions.
/// </summary>
/// <typeparam name="TCovenant">The covenant being configured.</typeparam>
/// <typeparam name="TIn">The input entry type being routed.</typeparam>
internal sealed class JunctionBuilder<TCovenant, TIn> : IJunctionBuilder<TCovenant, TIn>
    where TCovenant : ICovenant
    where TIn : ICovenantEntry<TCovenant>
{
    private readonly List<JunctionRoute> _routes = [];

    /// <summary>
    /// Gets the collected routes.
    /// </summary>
    public IReadOnlyList<JunctionRoute> Routes => _routes;

    /// <summary>
    /// Gets the fallback route, if configured.
    /// </summary>
    public JunctionRoute? FallbackRoute { get; private set; }

    /// <inheritdoc />
    public IJunctionBuilder<TCovenant, TIn> Route<TOut>(
        Func<TIn, bool> predicate,
        Func<TIn, TOut> transform)
        where TOut : ICovenantEntry<TCovenant>
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(transform);

        _routes.Add(new JunctionRoute
        {
            OutputType = typeof(TOut),
            Predicate = predicate,
            Transform = transform,
            IsMany = false
        });

        return this;
    }

    /// <inheritdoc />
    public IJunctionBuilder<TCovenant, TIn> RouteMany<TOut>(
        Func<TIn, bool> predicate,
        Func<TIn, IEnumerable<TOut>> transform)
        where TOut : ICovenantEntry<TCovenant>
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(transform);

        _routes.Add(new JunctionRoute
        {
            OutputType = typeof(TOut),
            Predicate = predicate,
            Transform = transform,
            IsMany = true
        });

        return this;
    }

    /// <inheritdoc />
    public IJunctionBuilder<TCovenant, TIn> Fallback<TOut>(
        Func<TIn, TOut> transform)
        where TOut : ICovenantEntry<TCovenant>
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (FallbackRoute is not null)
        {
            throw new InvalidOperationException("A fallback route has already been configured.");
        }

        FallbackRoute = new JunctionRoute
        {
            OutputType = typeof(TOut),
            Predicate = null,  // Fallback route matches when no other predicate applies
            Transform = transform,
            IsMany = false
        };

        return this;
    }
}
