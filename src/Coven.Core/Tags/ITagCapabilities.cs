// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

/// <summary>
/// Optional interface for blocks to advertise supported capability tags.
/// </summary>
public interface ITagCapabilities
{
    /// <summary>Capability tags supported by the block.</summary>
    IReadOnlyCollection<string> SupportedTags { get; }
}
