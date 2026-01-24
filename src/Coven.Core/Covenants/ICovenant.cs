// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Defines a journal protocol with compile-time connectivity guarantees.
/// A Covenant declares that all entry flows are statically verifiable:
/// no dead letters, no orphaned consumers, and a fully connected graph.
/// </summary>
/// <remarks>
/// Implement this interface as a sealed class with a static <see cref="Name"/> property.
/// <code>
/// public sealed class ChatCovenant : ICovenant
/// {
///     public static string Name =&gt; "Chat";
/// }
/// </code>
/// </remarks>
public interface ICovenant
{
    /// <summary>
    /// The human-readable name of this covenant (for diagnostics and logging).
    /// </summary>
    static abstract string Name { get; }
}
