using Coven.Core;

namespace Coven.Core.Builder;

public interface IMagikBuilder<T, TOutput>
{
    // Heterogeneous registration: allow adding blocks with any input/output
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string>? capabilities = null);

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func, IEnumerable<string>? capabilities = null);

    public ICoven Done();
    public ICoven Done(bool pull, PullOptions? pullOptions = null);
}
