using System;
using System.Collections.Generic;
using Coven.Core.Algos;
using Xunit;

namespace Coven.Core.Tests;

public class GraphSearchTests
{
    [Fact]
    public void BfsEdges_FindsShortest_PathWithDeterministicOrdering()
    {
        // Graph: 1 -> 2 ("a"), 2 -> 4 ("b"), 1 -> 3 ("c"), 3 -> 4 ("d")
        IEnumerable<(int next, string edge)> Expand(int n) => n switch
        {
            1 => new[] { (2, "a"), (3, "c") },
            2 => new[] { (4, "b") },
            3 => new[] { (4, "d") },
            _ => Array.Empty<(int, string)>()
        };

        // Order neighbors by the edge value to make the choice deterministic: "a" before "c".
        var path = GraphSearch.BfsEdges<int, string>(
            start: 1,
            isGoal: n => n == 4,
            expand: Expand,
            comparer: null,
            maxDepth: 10,
            orderNeighbors: (current, nbrs) => System.Linq.Enumerable.OrderBy(nbrs, x => x.edge)
        );

        Assert.NotNull(path);
        Assert.Collection(path!,
            e => Assert.Equal("a", e),
            e => Assert.Equal("b", e)
        );
    }

    [Fact]
    public void Annotated_BfsEdges_Equals_NonAnnotated_WithOrder()
    {
        // Graph:
        // 1 -> 3 ("c"), 1 -> 2 ("a")  // Note order: 3 first to prove ordering is applied
        // 2 -> 4 ("b"), 3 -> 5 ("d"), 4 -> 6 ("e"), 5 -> 6 ("f")
        var adj = new Dictionary<int, (int next, string edge)[]>
        {
            [1] = new[] { (3, "c"), (2, "a") },
            [2] = new[] { (4, "b") },
            [3] = new[] { (5, "d") },
            [4] = new[] { (6, "e") },
            [5] = new[] { (6, "f") },
        };

        IEnumerable<(int next, string edge)> Expand(int n) => adj.TryGetValue(n, out var list) ? list : Array.Empty<(int, string)>();
        IEnumerable<int> Nexts(int n)
        {
            if (adj.TryGetValue(n, out var list))
            {
                foreach (var (next, _) in list) yield return next;
            }
        }

        // Build reverse adjacency for reverse-distance precompute from refNode
        var rev = new Dictionary<int, List<int>>();
        foreach (var (from, list) in adj)
        {
            foreach (var (to, _) in list)
            {
                if (!rev.TryGetValue(to, out var l)) rev[to] = l = new List<int>();
                l.Add(from);
            }
        }
        IEnumerable<int> Prev(int n) => rev.TryGetValue(n, out var list) ? list : Array.Empty<int>();

        int refNode = 4; // prefer neighbors that are closer to 4

        // Non-annotated: compute distance-from-neighbor to refNode on the fly
        var nonAnn = GraphSearch.BfsEdges<int, string>(
            start: 1,
            isGoal: n => n == 6,
            expand: Expand,
            comparer: null,
            maxDepth: 10,
            orderNeighbors: (current, nbrs) => System.Linq.Enumerable.OrderBy(
                nbrs,
                x => Distance.MinHops(x.next, refNode, Nexts)
            )
        );

        // Annotated: build a reverse distance map (to refNode) once per node; here we just compute a global map and reuse it
        var ann = GraphSearch.BfsEdges<int, string, Dictionary<int, int>>(
            start: 1,
            isGoal: n => n == 6,
            expand: Expand,
            buildAnnotation: _ => Distance.AllMinHops(refNode, Prev), // reverse-graph distances to refNode
            orderNeighbors: (current, distToRef, nbrs) => System.Linq.Enumerable.OrderBy(
                nbrs,
                x => distToRef.TryGetValue(x.next, out var d) ? d : int.MaxValue
            ),
            comparer: null,
            maxDepth: 10
        );

        Assert.NotNull(nonAnn);
        Assert.NotNull(ann);

        // Expected to favor 1->2->4->6 due to refNode=4 proximity
        var expected = new[] { "a", "b", "e" };
        Assert.Equal(expected, nonAnn);
        Assert.Equal(expected, ann);
    }
}
