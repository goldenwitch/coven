namespace Coven.Transmutation;

public interface IBiDirectionalTransmuter<TIn, TOut>
{
    Task<TOut> TransmuteIn(TIn Input);
    Task<TIn> TransmuteOut(TOut Output);
}
