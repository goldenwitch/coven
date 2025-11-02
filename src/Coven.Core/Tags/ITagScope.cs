// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

/// <summary>
/// Represents a mutable tag scope used during a ritual to steer selection and record observations.
/// </summary>
public interface ITagScope
{
    /// <summary>The backing set for tags in this scope.</summary>
    ISet<string> TagSet { get; }
    /// <summary>Adds a tag to the current scope.</summary>
    void Add(string tag);
    /// <summary>Returns true if a tag exists in the current scope.</summary>
    bool Contains(string tag);
    /// <summary>Enumerates tags in the current scope.</summary>
    IEnumerable<string> Enumerate();
}
