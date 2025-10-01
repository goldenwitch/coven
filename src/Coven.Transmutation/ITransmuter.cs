namespace Coven.Transmutation;

public interface ITransmuter<TIn, TOut>
{
    Task<TOut> Transmute(TIn Input);
}