using System;
using System.Collections.Generic;

namespace Coven.Core.Algos;

public static class GraphSearch
{
    // Node-path BFS: returns the sequence of nodes from start to goal (inclusive end, exclusive start if you prefer adjust outside).
    public static List<TNode>? BfsNodes<TNode>(
        TNode start,
        Func<TNode, bool> isGoal,
        Func<TNode, IEnumerable<TNode>> neighbors,
        IEqualityComparer<TNode>? comparer = null,
        int maxDepth = int.MaxValue,
        Func<IEnumerable<TNode>, IEnumerable<TNode>>? order = null)
    {
        comparer ??= EqualityComparer<TNode>.Default;
        var visited = new HashSet<TNode>(comparer) { start };
        var q = new Queue<(TNode node, List<TNode> path)>();
        q.Enqueue((start, new List<TNode>()));

        while (q.Count > 0)
        {
            var (current, path) = q.Dequeue();
            if (isGoal(current))
            {
                return path;
            }
            if (path.Count >= maxDepth) continue;

            var nbrs = neighbors(current);
            if (order is not null) nbrs = order(nbrs);

            foreach (var n in nbrs)
            {
                if (visited.Add(n))
                {
                    var nextPath = new List<TNode>(path) { n };
                    q.Enqueue((n, nextPath));
                }
            }
        }

        return null;
    }

    // Edge-path BFS: returns the sequence of edges leading from start to a goal node.
    public static List<TEdge>? BfsEdges<TNode, TEdge>(
        TNode start,
        Func<TNode, bool> isGoal,
        Func<TNode, IEnumerable<(TNode next, TEdge edge)>> expand,
        IEqualityComparer<TNode>? comparer = null,
        int maxDepth = int.MaxValue,
        Func<TNode, IEnumerable<(TNode next, TEdge edge)>, IEnumerable<(TNode next, TEdge edge)>>? orderNeighbors = null)
    {
        comparer ??= EqualityComparer<TNode>.Default;
        var visited = new HashSet<TNode>(comparer) { start };
        var q = new Queue<(TNode node, List<TEdge> path)>();
        q.Enqueue((start, new List<TEdge>()));

        while (q.Count > 0)
        {
            var (current, path) = q.Dequeue();
            if (isGoal(current))
            {
                return path;
            }
            if (path.Count >= maxDepth) continue;

            var nbrs = expand(current);
            if (orderNeighbors is not null) nbrs = orderNeighbors(current, nbrs);

            foreach (var (next, edge) in nbrs)
            {
                if (visited.Add(next))
                {
                    var nextPath = new List<TEdge>(path) { edge };
                    q.Enqueue((next, nextPath));
                }
            }
        }

        return null;
    }

    // Annotated edge-path BFS: builds a per-node annotation once, then uses it to order neighbors.
    public static List<TEdge>? BfsEdges<TNode, TEdge, TAnno>(
        TNode start,
        Func<TNode, bool> isGoal,
        Func<TNode, IEnumerable<(TNode next, TEdge edge)>> expand,
        Func<TNode, TAnno> buildAnnotation,
        Func<TNode, TAnno, IEnumerable<(TNode next, TEdge edge)>, IEnumerable<(TNode next, TEdge edge)>> orderNeighbors,
        IEqualityComparer<TNode>? comparer = null,
        int maxDepth = int.MaxValue)
    {
        comparer ??= EqualityComparer<TNode>.Default;
        var visited = new HashSet<TNode>(comparer) { start };
        var q = new Queue<(TNode node, List<TEdge> path)>();
        q.Enqueue((start, new List<TEdge>()));

        while (q.Count > 0)
        {
            var (current, path) = q.Dequeue();
            if (isGoal(current))
            {
                return path;
            }
            if (path.Count >= maxDepth) continue;

            var anno = buildAnnotation(current);
            var ordered = orderNeighbors(current, anno, expand(current));
            foreach (var (next, edge) in ordered)
            {
                if (visited.Add(next))
                {
                    var nextPath = new List<TEdge>(path) { edge };
                    q.Enqueue((next, nextPath));
                }
            }
        }

        return null;
    }
}
