// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;

namespace Coven.Covenants;

/// <summary>
/// Metadata about a registered edge in the covenant graph.
/// </summary>
public abstract record CovenantEdge
{
    /// <summary>
    /// The input type consumed by this edge.
    /// </summary>
    public required Type InputType { get; init; }

    /// <summary>
    /// The output type produced by this edge (null for sources/sinks).
    /// </summary>
    public required Type? OutputType { get; init; }
}

/// <summary>
/// A source boundary in the covenant graph.
/// </summary>
public sealed record CovenantSourceEdge : CovenantEdge;

/// <summary>
/// A sink boundary in the covenant graph.
/// </summary>
public sealed record CovenantSinkEdge : CovenantEdge;

/// <summary>
/// A window operation in the covenant graph.
/// </summary>
public sealed record CovenantWindowEdge : CovenantEdge
{
    /// <summary>
    /// The window policy instance.
    /// </summary>
    public required object Policy { get; init; }

    /// <summary>
    /// The batch transmuter instance.
    /// </summary>
    public required object Transmuter { get; init; }

    /// <summary>
    /// Optional shatter policy instance.
    /// </summary>
    public object? Shatter { get; init; }
}

/// <summary>
/// A 1:1 transform operation in the covenant graph.
/// </summary>
public sealed record CovenantTransformEdge : CovenantEdge
{
    /// <summary>
    /// The transmuter instance.
    /// </summary>
    public required object Transmuter { get; init; }
}

/// <summary>
/// A junction operation that routes a single input type to multiple output types.
/// </summary>
public sealed record CovenantJunctionEdge : CovenantEdge
{
    /// <summary>
    /// The predicated routes for this junction.
    /// </summary>
    public required IReadOnlyList<JunctionRoute> Routes { get; init; }

    /// <summary>
    /// Optional fallback route for entries that don't match any predicate.
    /// </summary>
    public JunctionRoute? FallbackRoute { get; init; }
}

/// <summary>
/// A single route within a junction.
/// </summary>
public sealed record JunctionRoute
{
    /// <summary>
    /// The output type produced by this route.
    /// </summary>
    public required Type OutputType { get; init; }

    /// <summary>
    /// The predicate that determines if this route applies (Func&lt;TIn, bool&gt;).
    /// Null for fallback routes that match when no other predicate applies.
    /// </summary>
    public object? Predicate { get; init; }

    /// <summary>
    /// The transform function (Func&lt;TIn, TOut&gt; or Func&lt;TIn, IEnumerable&lt;TOut&gt;&gt;).
    /// </summary>
    public required object Transform { get; init; }

    /// <summary>
    /// True if the transform produces multiple outputs (RouteMany).
    /// </summary>
    public required bool IsMany { get; init; }
}

/// <summary>
/// Collected metadata about a covenant's entry flow graph.
/// Used for runtime validation and by the Roslyn analyzer.
/// </summary>
/// <typeparam name="TCovenant">The covenant type.</typeparam>
public sealed class CovenantGraph<TCovenant> where TCovenant : ICovenant
{
    private readonly List<CovenantEdge> _edges = [];

    /// <summary>
    /// All edges in the covenant graph.
    /// </summary>
    public IReadOnlyList<CovenantEdge> Edges => _edges;

    /// <summary>
    /// All declared source types.
    /// </summary>
    public IEnumerable<Type> Sources => _edges.OfType<CovenantSourceEdge>().Select(e => e.InputType);

    /// <summary>
    /// All declared sink types.
    /// </summary>
    public IEnumerable<Type> Sinks => _edges.OfType<CovenantSinkEdge>().Select(e => e.InputType);

    /// <summary>
    /// All window operations.
    /// </summary>
    public IEnumerable<CovenantWindowEdge> Windows => _edges.OfType<CovenantWindowEdge>();

    /// <summary>
    /// All transform operations.
    /// </summary>
    public IEnumerable<CovenantTransformEdge> Transforms => _edges.OfType<CovenantTransformEdge>();

    /// <summary>
    /// All junction operations.
    /// </summary>
    public IEnumerable<CovenantJunctionEdge> Junctions => _edges.OfType<CovenantJunctionEdge>();

    internal void AddSource(Type entryType)
    {
        _edges.Add(new CovenantSourceEdge { InputType = entryType, OutputType = null });
    }

    internal void AddSink(Type entryType)
    {
        _edges.Add(new CovenantSinkEdge { InputType = entryType, OutputType = null });
    }

    internal void AddWindow(Type inputType, Type outputType, object policy, object transmuter, object? shatter)
    {
        _edges.Add(new CovenantWindowEdge
        {
            InputType = inputType,
            OutputType = outputType,
            Policy = policy,
            Transmuter = transmuter,
            Shatter = shatter
        });
    }

    internal void AddTransform(Type inputType, Type outputType, object transmuter)
    {
        _edges.Add(new CovenantTransformEdge
        {
            InputType = inputType,
            OutputType = outputType,
            Transmuter = transmuter
        });
    }

    internal void AddJunction(Type inputType, IReadOnlyList<JunctionRoute> routes, JunctionRoute? fallbackRoute)
    {
        _edges.Add(new CovenantJunctionEdge
        {
            InputType = inputType,
            OutputType = null,  // Junctions have multiple outputs via routes
            Routes = routes,
            FallbackRoute = fallbackRoute
        });
    }
}
