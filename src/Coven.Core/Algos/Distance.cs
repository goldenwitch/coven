using System;
using System.Collections.Generic;

namespace Coven.Core.Algos;

public static class Distance
{
    // Returns the minimal number of hops from `start` to `goal` using BFS over `neighbors`.
    // If `goal` is not reachable within `maxDepth`, returns int.MaxValue.
    public static int MinHops<T>(
        T start,
        T goal,
        Func<T, IEnumerable<T>> neighbors,
        IEqualityComparer<T>? comparer = null,
        int maxDepth = int.MaxValue)
        where T : notnull
    {
        comparer ??= EqualityComparer<T>.Default;
        if (comparer.Equals(start, goal)) return 0;

        var visited = new HashSet<T>(comparer) { start };
        var q = new Queue<(T node, int dist)>();
        q.Enqueue((start, 0));

        while (q.Count > 0)
        {
            var (current, d) = q.Dequeue();
            if (d >= maxDepth) continue;

            foreach (var n in neighbors(current))
            {
                if (!visited.Add(n)) continue;
                var nd = d + 1;
                if (comparer.Equals(n, goal)) return nd;
                q.Enqueue((n, nd));
            }
        }

        return int.MaxValue;
    }

    // Returns a dictionary of minimal hops from `start` to every reachable node (including start with distance 0).
    public static Dictionary<T, int> AllMinHops<T>(
        T start,
        Func<T, IEnumerable<T>> neighbors,
        IEqualityComparer<T>? comparer = null,
        int maxDepth = int.MaxValue)
        where T : notnull
    {
        comparer ??= EqualityComparer<T>.Default;
        var dist = new Dictionary<T, int>(comparer) { [start] = 0 };
        var q = new Queue<T>();
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var current = q.Dequeue();
            var d = dist[current];
            if (d >= maxDepth) continue;

            foreach (var n in neighbors(current))
            {
                if (dist.ContainsKey(n)) continue;
                dist[n] = d + 1;
                q.Enqueue(n);
            }
        }

        return dist;
    }
}
