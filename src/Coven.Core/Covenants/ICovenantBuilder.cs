// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// Base interface for covenant builders.
/// Provides boundary declarations that don't depend on streaming/transmutation.
/// </summary>
/// <typeparam name="TCovenant">The covenant being configured.</typeparam>
/// <remarks>
/// The full builder with Window/Transform operations is in <c>Coven.Covenants</c>.
/// This base interface lives in Core so entry types can be decorated with markers
/// without pulling in streaming dependencies.
/// </remarks>
public interface ICovenantBuilder<TCovenant> where TCovenant : ICovenant
{
    /// <summary>
    /// Declare an entry type that enters from outside the covenant.
    /// </summary>
    /// <typeparam name="TEntry">The source entry type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    ICovenantBuilder<TCovenant> Source<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSource<TCovenant>;

    /// <summary>
    /// Declare an entry type that exits to outside the covenant.
    /// </summary>
    /// <typeparam name="TEntry">The sink entry type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    ICovenantBuilder<TCovenant> Sink<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSink<TCovenant>;
}
