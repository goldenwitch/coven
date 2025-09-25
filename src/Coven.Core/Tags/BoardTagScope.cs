// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

internal sealed class BoardTagScope : ITagScope
{
    private readonly List<(string Tag, int Epoch)> _journal = [];
    private readonly List<string> _logs = [];

    internal int Epoch { get; private set; }


    internal BoardTagScope(IEnumerable<string>? initial = null)
    {
        TagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (initial is not null)
        {
            foreach (string t in initial)
            {
                if (TagSet.Add(t))
                {
                    _journal.Add((t, Epoch));
                }

            }
        }
    }

    public ISet<string> TagSet { get; }

    public void Add(string tag)
    {
        if (TagSet.Add(tag))
        {
            _journal.Add((tag, Epoch));
        }

    }

    public bool Contains(string tag) => TagSet.Contains(tag);

    public IEnumerable<string> Enumerate() => TagSet;

    internal void IncrementEpoch() => Epoch++;

    internal IReadOnlyList<string> GetCurrentEpochTags()
        => _journal.Count == 0
            ? Array.Empty<string>()
            : _journal.Where(e => e.Epoch == Epoch)
                     .Select(e => e.Tag)
                     .ToList();

    internal void AddLog(string message)
        => _logs.Add($"[e{Epoch}] {message}");

    internal IReadOnlyList<string> GetLogs() => _logs;
}