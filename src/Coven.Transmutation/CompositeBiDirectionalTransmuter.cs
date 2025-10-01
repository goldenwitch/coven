namespace Coven.Transmutation;

public class CompositeBiDirectionalTransmuter<TIn, TOut>(ITransmuter<TIn, TOut> InTramsuter, ITransmuter<TOut, TIn> OutTransmuter) : IBiDirectionalTransmuter<TIn, TOut>
{
    private readonly ITransmuter<TIn, TOut> _inTransmuter = InTramsuter;
    private readonly ITransmuter<TOut, TIn> _outTransmuter = OutTransmuter;

    public Task<TOut> TransmuteIn(TIn Input)
    {
        return _inTransmuter.Transmute(Input);
    }

    public Task<TIn> TransmuteOut(TOut Output)
    {
        return _outTransmuter.Transmute(Output);
    }
}
