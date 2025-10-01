namespace Coven.Transmutation;

public interface IBiDirectionalTransmuter<TIn, TOut>
{
    Task<TOut> TransmuteIn(TIn Input, CancellationToken cancellationToken = default);
    Task<TIn> TransmuteOut(TOut Output, CancellationToken cancellationToken = default);
}
