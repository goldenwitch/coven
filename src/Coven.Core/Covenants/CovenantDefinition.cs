// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Covenants;

/// <summary>
/// Collects route and terminal definitions during covenant configuration.
/// </summary>
internal sealed class CovenantDefinition : ICovenant
{
    private readonly List<RouteDescriptor> _routes = [];
    private readonly List<TerminalDescriptor> _terminals = [];

    /// <summary>
    /// All routes defined in this covenant.
    /// </summary>
    internal IReadOnlyList<RouteDescriptor> Routes => _routes;

    /// <summary>
    /// All terminal types defined in this covenant.
    /// </summary>
    internal IReadOnlyList<TerminalDescriptor> Terminals => _terminals;

    /// <inheritdoc />
    public ICovenant Route<TSource, TTarget>(Func<TSource, CancellationToken, Task<TTarget>> map)
        where TSource : Entry
        where TTarget : Entry
    {
        ArgumentNullException.ThrowIfNull(map);

        // Capture transformation in closure—no reflection at invocation
        async Task<Entry> InvokerAsync(Entry entry, CancellationToken ct)
        {
            return await map((TSource)entry, ct);
        }

        _routes.Add(new LambdaRouteDescriptor(typeof(TSource), typeof(TTarget), InvokerAsync));
        return this;
    }

    /// <inheritdoc />
    public ICovenant Route<TSource, TTarget, TTransmuter>()
        where TSource : Entry
        where TTarget : Entry
        where TTransmuter : class, ITransmuter<TSource, TTarget>
    {
        // Defer resolution until we have a service provider
        Func<Entry, CancellationToken, Task<Entry>> CreateInvoker(IServiceProvider sp)
        {
            TTransmuter transmuter = sp.GetRequiredService<TTransmuter>();
            async Task<Entry> InvokerAsync(Entry entry, CancellationToken ct)
            {
                return await transmuter.Transmute((TSource)entry, ct);
            }
            return InvokerAsync;
        }

        _routes.Add(new TransmuterRouteDescriptor(
            typeof(TSource), typeof(TTarget), typeof(TTransmuter), CreateInvoker));
        return this;
    }

    /// <inheritdoc />
    public ICovenant Terminal<TEntry>()
        where TEntry : Entry
    {
        _terminals.Add(new TerminalDescriptor(typeof(TEntry)));
        return this;
    }
}

/// <summary>
/// Base class for route descriptors.
/// </summary>
internal abstract record RouteDescriptor(Type SourceType, Type TargetType);

/// <summary>
/// A route defined by an async lambda transformation.
/// </summary>
internal sealed record LambdaRouteDescriptor(
    Type SourceType,
    Type TargetType,
    Func<Entry, CancellationToken, Task<Entry>> Invoke)
    : RouteDescriptor(SourceType, TargetType);

/// <summary>
/// A route defined by a DI-resolved transmuter.
/// </summary>
internal sealed record TransmuterRouteDescriptor(
    Type SourceType,
    Type TargetType,
    Type TransmuterType,
    Func<IServiceProvider, Func<Entry, CancellationToken, Task<Entry>>> CreateInvoker)
    : RouteDescriptor(SourceType, TargetType);

/// <summary>
/// A terminal declaration for a type that is explicitly not routed.
/// </summary>
internal sealed record TerminalDescriptor(Type SourceType);
