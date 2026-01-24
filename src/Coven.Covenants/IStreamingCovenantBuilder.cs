// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Streaming;
using Coven.Transmutation;

namespace Coven.Covenants;

/// <summary>
/// Extended covenant builder with windowing and transform operations.
/// Inherits boundary declarations from <see cref="ICovenantBuilder{TCovenant}"/>.
/// </summary>
/// <typeparam name="TCovenant">The covenant being configured.</typeparam>
public interface IStreamingCovenantBuilder<TCovenant> : ICovenantBuilder<TCovenant>
    where TCovenant : ICovenant
{
    /// <summary>
    /// Wire a windowing pipeline using existing primitives.
    /// </summary>
    /// <typeparam name="TChunk">The input chunk type.</typeparam>
    /// <typeparam name="TOutput">The output type after transmutation.</typeparam>
    /// <param name="policy">The window policy that decides when to emit.</param>
    /// <param name="transmuter">The batch transmuter that transforms windows.</param>
    /// <param name="shatter">Optional shatter policy for post-transform split.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IStreamingCovenantBuilder<TCovenant> Window<TChunk, TOutput>(
        IWindowPolicy<TChunk> policy,
        IBatchTransmuter<TChunk, TOutput> transmuter,
        IShatterPolicy<TOutput>? shatter = null)
        where TChunk : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>;

    /// <summary>
    /// Wire a 1:1 transform between entry types.
    /// </summary>
    /// <typeparam name="TInput">The input entry type.</typeparam>
    /// <typeparam name="TOutput">The output entry type.</typeparam>
    /// <param name="transmuter">The transmuter that performs the transform.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IStreamingCovenantBuilder<TCovenant> Transform<TInput, TOutput>(
        ITransmuter<TInput, TOutput> transmuter)
        where TInput : ICovenantEntry<TCovenant>
        where TOutput : ICovenantEntry<TCovenant>;

    /// <summary>
    /// Wire a junction that routes a single input type to multiple output types based on predicates.
    /// </summary>
    /// <typeparam name="TIn">The input entry type being routed.</typeparam>
    /// <param name="configure">Action to configure the junction routes.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IStreamingCovenantBuilder<TCovenant> Junction<TIn>(
        Action<IJunctionBuilder<TCovenant, TIn>> configure)
        where TIn : ICovenantEntry<TCovenant>;

    /// <summary>
    /// Hides the base Source to return the extended builder type.
    /// </summary>
    new IStreamingCovenantBuilder<TCovenant> Source<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSource<TCovenant>;

    /// <summary>
    /// Hides the base Sink to return the extended builder type.
    /// </summary>
    new IStreamingCovenantBuilder<TCovenant> Sink<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSink<TCovenant>;
}
