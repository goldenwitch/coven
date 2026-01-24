// SPDX-License-Identifier: BUSL-1.1

using System.Reflection;
using Coven.Core.Covenants;

namespace Coven.Covenants.Tests.Infrastructure;

/// <summary>
/// Helper to build <see cref="CovenantGraph{TCovenant}"/> instances for testing.
/// Uses reflection to access internal Add* methods.
/// </summary>
public static class TestGraphBuilder
{
    /// <summary>
    /// Creates a new graph builder for the test covenant.
    /// </summary>
    public static GraphBuilder<TestCovenant> Create() => new();
}

/// <summary>
/// Fluent builder for test graphs.
/// </summary>
public sealed class GraphBuilder<TCovenant> where TCovenant : ICovenant
{
    private readonly CovenantGraph<TCovenant> _graph = new();

    private static readonly MethodInfo _addSource = typeof(CovenantGraph<TCovenant>)
        .GetMethod("AddSource", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _addSink = typeof(CovenantGraph<TCovenant>)
        .GetMethod("AddSink", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _addWindow = typeof(CovenantGraph<TCovenant>)
        .GetMethod("AddWindow", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _addTransform = typeof(CovenantGraph<TCovenant>)
        .GetMethod("AddTransform", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _addJunction = typeof(CovenantGraph<TCovenant>)
        .GetMethod("AddJunction", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Add a source type to the graph.
    /// </summary>
    public GraphBuilder<TCovenant> WithSource<TEntry>() where TEntry : ICovenantEntry<TCovenant>
    {
        _addSource.Invoke(_graph, [typeof(TEntry)]);
        return this;
    }

    /// <summary>
    /// Add a sink type to the graph.
    /// </summary>
    public GraphBuilder<TCovenant> WithSink<TEntry>() where TEntry : ICovenantEntry<TCovenant>
    {
        _addSink.Invoke(_graph, [typeof(TEntry)]);
        return this;
    }

    /// <summary>
    /// Add a transform edge to the graph.
    /// </summary>
    public GraphBuilder<TCovenant> WithTransform<TInput, TOutput>()
        where TInput : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>
    {
        _addTransform.Invoke(_graph, [typeof(TInput), typeof(TOutput), new object()]);
        return this;
    }

    /// <summary>
    /// Add a window edge to the graph.
    /// </summary>
    public GraphBuilder<TCovenant> WithWindow<TInput, TOutput>()
        where TInput : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>
    {
        _addWindow.Invoke(_graph, [typeof(TInput), typeof(TOutput), new object(), new object(), null]);
        return this;
    }

    /// <summary>
    /// Add a junction edge to the graph.
    /// </summary>
    public GraphBuilder<TCovenant> WithJunction<TInput>(params Type[] outputTypes)
        where TInput : ICovenantEntry<TCovenant>
    {
        List<JunctionRoute> routes =
        [
            .. outputTypes.Select(t => new JunctionRoute
            {
                OutputType = t,
                Predicate = new Func<object, bool>(_ => true),
                Transform = new Func<object, object>(x => x),
                IsMany = false
            })
        ];

        _addJunction.Invoke(_graph, [typeof(TInput), routes, null]);
        return this;
    }

    /// <summary>
    /// Add a junction with fallback to the graph.
    /// </summary>
    public GraphBuilder<TCovenant> WithJunctionAndFallback<TInput, TFallback>(params Type[] outputTypes)
        where TInput : ICovenantEntry<TCovenant>
        where TFallback : ICovenantEntry<TCovenant>
    {
        List<JunctionRoute> routes =
        [
            .. outputTypes.Select(t => new JunctionRoute
            {
                OutputType = t,
                Predicate = new Func<object, bool>(_ => true),
                Transform = new Func<object, object>(x => x),
                IsMany = false
            })
        ];

        JunctionRoute fallback = new()
        {
            OutputType = typeof(TFallback),
            Predicate = null,  // Fallback has no predicate
            Transform = new Func<object, object>(x => x),
            IsMany = false
        };

        _addJunction.Invoke(_graph, [typeof(TInput), routes, fallback]);
        return this;
    }

    /// <summary>
    /// Build the graph.
    /// </summary>
    public CovenantGraph<TCovenant> Build() => _graph;
}
