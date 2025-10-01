namespace Coven.Transmutation;

/// <summary>
/// Defines a one-way transmutation from <typeparamref name="TIn"/> to <typeparamref name="TOut"/>,
/// optionally observing a <see cref="CancellationToken"/> for cooperative cancellation.
/// </summary>
/// <typeparam name="TIn">The input type accepted by the transmutation.</typeparam>
/// <typeparam name="TOut">The output type produced by the transmutation.</typeparam>
public interface ITransmuter<TIn, TOut>
{
    /// <summary>
    /// Transmutes the given <paramref name="Input"/> into an instance of <typeparamref name="TOut"/>.
    /// </summary>
    /// <param name="Input">The input value to transmute.</param>
    /// <param name="cancellationToken">Token to observe for cooperative cancellation.</param>
    /// <returns>A task that completes with the transmuted <typeparamref name="TOut"/>.</returns>
    /// <remarks>
    /// Implementations should avoid side-effects and propagate <see cref="OperationCanceledException"/>
    /// when <paramref name="cancellationToken"/> is canceled. Exceptions thrown by the transmutation
    /// should flow directly to the caller.
    /// </remarks>
    Task<TOut> Transmute(TIn Input, CancellationToken cancellationToken = default);
}
