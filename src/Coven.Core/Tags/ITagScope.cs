// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

public interface ITagScope
{
    ISet<string> TagSet { get; }
    void Add(string tag);
    bool Contains(string tag);
    IEnumerable<string> Enumerate();
}