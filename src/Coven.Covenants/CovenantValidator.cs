// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;

namespace Coven.Covenants;

/// <summary>
/// Runtime validator for covenant graphs.
/// Checks the same invariants as the Roslyn analyzer, but at startup.
/// </summary>
public static class CovenantValidator
{
    /// <summary>
    /// Validates that a covenant graph is complete and connected.
    /// </summary>
    /// <typeparam name="TCovenant">The covenant type.</typeparam>
    /// <param name="graph">The graph to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
    public static void Validate<TCovenant>(CovenantGraph<TCovenant> graph)
        where TCovenant : ICovenant
    {
        ArgumentNullException.ThrowIfNull(graph);

        List<string> errors = [];

        // Collect all entry types that participate in the covenant
        HashSet<Type> allTypes = [];
        HashSet<Type> sources = [];
        HashSet<Type> sinks = [];
        HashSet<Type> consumed = [];  // Types that are inputs to operations
        HashSet<Type> produced = [];  // Types that are outputs of operations

        foreach (Type source in graph.Sources)
        {
            allTypes.Add(source);
            sources.Add(source);
            produced.Add(source);  // Sources "produce" entries from outside
        }

        foreach (Type sink in graph.Sinks)
        {
            allTypes.Add(sink);
            sinks.Add(sink);
            consumed.Add(sink);  // Sinks "consume" entries to outside
        }

        foreach (CovenantWindowEdge window in graph.Windows)
        {
            allTypes.Add(window.InputType);
            if (window.OutputType is not null)
            {
                allTypes.Add(window.OutputType);
            }
            consumed.Add(window.InputType);
            if (window.OutputType is not null)
            {
                produced.Add(window.OutputType);
            }
        }

        foreach (CovenantTransformEdge transform in graph.Transforms)
        {
            allTypes.Add(transform.InputType);
            if (transform.OutputType is not null)
            {
                allTypes.Add(transform.OutputType);
            }
            consumed.Add(transform.InputType);
            if (transform.OutputType is not null)
            {
                produced.Add(transform.OutputType);
            }
        }

        foreach (CovenantJunctionEdge junction in graph.Junctions)
        {
            allTypes.Add(junction.InputType);
            consumed.Add(junction.InputType);

            // Each route produces its output type
            foreach (JunctionRoute route in junction.Routes)
            {
                allTypes.Add(route.OutputType);
                produced.Add(route.OutputType);
            }

            // Fallback route also produces its output type
            if (junction.FallbackRoute is not null)
            {
                allTypes.Add(junction.FallbackRoute.OutputType);
                produced.Add(junction.FallbackRoute.OutputType);
            }
        }

        // Check 1: No dead letters (every type must be consumed or be a sink)
        foreach (Type type in allTypes)
        {
            if (!consumed.Contains(type) && !sinks.Contains(type))
            {
                errors.Add($"Dead letter: {type.Name} is produced but never consumed and is not a sink.");
            }
        }

        // Check 2: No orphaned consumers (every consumed type must be produced or be a source)
        foreach (Type type in consumed)
        {
            if (!produced.Contains(type) && !sources.Contains(type))
            {
                errors.Add($"Orphaned consumer: {type.Name} is consumed but never produced and is not a source.");
            }
        }

        // Check 3: Graph must have at least one source and one sink
        if (sources.Count == 0)
        {
            errors.Add("Covenant has no sources. At least one entry must implement ICovenantSource<T>.");
        }

        if (sinks.Count == 0)
        {
            errors.Add("Covenant has no sinks. At least one entry must implement ICovenantSink<T>.");
        }

        // Check 4: Connectivity - simplified reachability check
        // From each source, can we reach at least one sink?
        if (sources.Count > 0 && sinks.Count > 0)
        {
            Dictionary<Type, HashSet<Type>> adjacency = BuildAdjacencyList(graph);
            HashSet<Type> reachableFromSources = [];

            foreach (Type source in sources)
            {
                FindReachable(source, adjacency, reachableFromSources);
            }

            foreach (Type sink in sinks)
            {
                if (!reachableFromSources.Contains(sink))
                {
                    errors.Add($"Island: Sink {sink.Name} is not reachable from any source.");
                }
            }
        }

        if (errors.Count > 0)
        {
            string covenantName = TCovenant.Name;
            throw new InvalidOperationException(
                $"Covenant '{covenantName}' validation failed:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    private static Dictionary<Type, HashSet<Type>> BuildAdjacencyList<TCovenant>(CovenantGraph<TCovenant> graph)
        where TCovenant : ICovenant
    {
        Dictionary<Type, HashSet<Type>> adjacency = [];

        void AddEdge(Type from, Type? to)
        {
            if (to is null)
            {
                return;
            }

            if (!adjacency.TryGetValue(from, out HashSet<Type>? neighbors))
            {
                neighbors = [];
                adjacency[from] = neighbors;
            }
            neighbors.Add(to);
        }

        foreach (CovenantWindowEdge window in graph.Windows)
        {
            AddEdge(window.InputType, window.OutputType);
        }

        foreach (CovenantTransformEdge transform in graph.Transforms)
        {
            AddEdge(transform.InputType, transform.OutputType);
        }

        foreach (CovenantJunctionEdge junction in graph.Junctions)
        {
            // Add edges from junction input to all route outputs
            foreach (JunctionRoute route in junction.Routes)
            {
                AddEdge(junction.InputType, route.OutputType);
            }

            // Add edge for fallback route if present
            if (junction.FallbackRoute is not null)
            {
                AddEdge(junction.InputType, junction.FallbackRoute.OutputType);
            }
        }

        return adjacency;
    }

    private static void FindReachable(Type start, Dictionary<Type, HashSet<Type>> adjacency, HashSet<Type> visited)
    {
        if (!visited.Add(start))
        {
            return;
        }

        if (adjacency.TryGetValue(start, out HashSet<Type>? neighbors))
        {
            foreach (Type neighbor in neighbors)
            {
                FindReachable(neighbor, adjacency, visited);
            }
        }
    }
}
