// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Transmutation;

/// <summary>
/// Defines a two-parameter transmutation where an input is "imbued" with a secondary
/// reagent to produce an output. Implementations should be pure and cancel-aware.
/// </summary>
/// <typeparam name="TIn">Primary input type.</typeparam>
/// <typeparam name="TReagent">Secondary input (reagent) type.</typeparam>
/// <typeparam name="TOut">Output type.</typeparam>
public interface IImbuingTransmuter<TIn, TReagent, TOut> : ITransmuter<(TIn Input, TReagent Reagent), TOut>
{
    /// <summary>
    /// Transmutes the given <paramref name="Input"/> using the provided <paramref name="Reagent"/>,
    /// producing an instance of <typeparamref name="TOut"/>.
    /// </summary>
    /// <param name="Input">Primary input value.</param>
    /// <param name="Reagent">Secondary input value that modifies the transmutation.</param>
    /// <param name="cancellationToken">Token to observe for cooperative cancellation.</param>
    /// <returns>A task that completes with the transmuted output.</returns>
    Task<TOut> Transmute(TIn Input, TReagent Reagent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tuple-based transmutation to align with <see cref="ITransmuter{TIn, TOut}"/>; forwards to the
    /// two-parameter overload for convenience and consistency.
    /// </summary>
    /// <param name="Input">A tuple containing the primary input and reagent.</param>
    /// <param name="cancellationToken">Token to observe for cooperative cancellation.</param>
    /// <returns>A task that completes with the transmuted output.</returns>
    Task<TOut> ITransmuter<(TIn Input, TReagent Reagent), TOut>.Transmute((TIn Input, TReagent Reagent) Input, CancellationToken cancellationToken)
        => Transmute(Input.Input, Input.Reagent, cancellationToken);
}
