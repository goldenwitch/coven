using System.Collections.Generic;

namespace Coven.Core.Tags;

internal sealed class BoardTagScope : ITagScope
{
    private readonly ISet<string> set;

    internal BoardTagScope(IEnumerable<string>? initial = null)
    {
        set = initial is null
            ? new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(initial, System.StringComparer.OrdinalIgnoreCase);
    }

    public ISet<string> Set => set;

    public void Add(string tag) => set.Add(tag);

    public bool Contains(string tag) => set.Contains(tag);

    public IEnumerable<string> Enumerate() => set;
}

