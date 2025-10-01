namespace Coven.Transmutation;

/// <summary>
/// Composes two one-way transmuters to provide a bidirectional adapter between
/// <typeparamref name="TIn"/> and <typeparamref name="TOut"/>.
/// </summary>
/// <typeparam name="TIn">The primary input type.</typeparam>
/// <typeparam name="TOut">The primary output type.</typeparam>
/// <param name="InTramsuter">Transmuter used for <see cref="TransmuteIn(TIn, CancellationToken)"/>.</param>
/// <param name="OutTransmuter">Transmuter used for <see cref="TransmuteOut(TOut, CancellationToken)"/>.</param>
public class CompositeBiDirectionalTransmuter<TIn, TOut>(ITransmuter<TIn, TOut> InTramsuter, ITransmuter<TOut, TIn> OutTransmuter) : IBiDirectionalTransmuter<TIn, TOut>
{
    private readonly ITransmuter<TIn, TOut> _inTransmuter = InTramsuter;
    private readonly ITransmuter<TOut, TIn> _outTransmuter = OutTransmuter;

    /// <inheritdoc />
    public Task<TOut> TransmuteIn(TIn Input, CancellationToken cancellationToken = default)
    {
        return _inTransmuter.Transmute(Input, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TIn> TransmuteOut(TOut Output, CancellationToken cancellationToken = default)
    {
        return _outTransmuter.Transmute(Output, cancellationToken);
    }
}
