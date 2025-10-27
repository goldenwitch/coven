namespace Coven.Transmutation;

/// <summary>
/// Defines a two-way transmutation between <typeparamref name="TIn"/> and <typeparamref name="TOut"/>,
/// exposing symmetric operations for converting in both directions.
/// </summary>
/// <typeparam name="TIn">The primary input type.</typeparam>
/// <typeparam name="TOut">The primary output type.</typeparam>
public interface IBiDirectionalTransmuter<TIn, TOut>
{
    /// <summary>
    /// Transmutes a value of <typeparamref name="TIn"/> into <typeparamref name="TOut"/>.
    /// </summary>
    /// <param name="Input">The input value.</param>
    /// <param name="cancellationToken">Token to observe for cooperative cancellation.</param>
    /// <returns>A task producing the transmuted <typeparamref name="TOut"/>.</returns>
    Task<TOut> TransmuteAfferent(TIn Input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transmutes a value of <typeparamref name="TOut"/> back to <typeparamref name="TIn"/>.
    /// </summary>
    /// <param name="Output">The output value to reverse-transmute.</param>
    /// <param name="cancellationToken">Token to observe for cooperative cancellation.</param>
    /// <returns>A task producing the transmuted <typeparamref name="TIn"/>.</returns>
    Task<TIn> TransmuteEfferent(TOut Output, CancellationToken cancellationToken = default);
}
