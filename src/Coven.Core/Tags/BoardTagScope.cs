// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

internal sealed class BoardTagScope : ITagScope
{
    private readonly ISet<string> set;
    private readonly List<(string Tag, int Epoch)> journal = new();
    private readonly List<string> logs = new();

    internal int Epoch { get; private set; } = 0;

    internal BoardTagScope(IEnumerable<string>? initial = null)
    {
        set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (initial is not null)
        {
            foreach (var t in initial)
            {
                if (set.Add(t)) journal.Add((t, Epoch));
            }
        }
    }

    public ISet<string> Set => set;

    public void Add(string tag)
    {
        if (set.Add(tag)) journal.Add((tag, Epoch));
    }

    public bool Contains(string tag) => set.Contains(tag);

    public IEnumerable<string> Enumerate() => set;

    internal void IncrementEpoch() => Epoch++;

    internal IReadOnlyList<string> GetCurrentEpochTags()
        => journal.Count == 0
            ? Array.Empty<string>()
            : journal.Where(e => e.Epoch == Epoch)
                     .Select(e => e.Tag)
                     .ToList();

    internal void AddLog(string message)
        => logs.Add($"[e{Epoch}] {message}");

    internal IReadOnlyList<string> GetLogs() => logs;
}