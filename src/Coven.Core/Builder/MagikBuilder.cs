// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Routing;

namespace Coven.Core.Builder;

internal class MagikBuilder<T, TOutput> : IMagikBuilder<T, TOutput>
{
    private readonly MagikRegistry _registry = new();

    public IMagikBuilder<T, TOutput> UseSelectionStrategy(ISelectionStrategy strategy)
    {
        _registry.SetSelectionStrategy(strategy);
        return this;
    }

    // Heterogeneous registration (with optional capabilities)
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string>? capabilities = null)
    {
        _registry.Add(new MagikBlockDescriptor(
            typeof(TIn),
            typeof(TOut),
            block,
            capabilities?.ToList(),
            block.GetType().Name));
        return this;
    }

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, CancellationToken, Task<TOut>> func, IEnumerable<string>? capabilities = null)
    {
        MagikBlock<TIn, TOut> mb = new(func);
        _registry.Add(new MagikBlockDescriptor(
            typeof(TIn),
            typeof(TOut),
            mb,
            capabilities?.ToList(),
            typeof(MagikBlock<TIn, TOut>).Name));
        return this;
    }

    public ICoven Done() => Done(pull: false, pullOptions: null);

    public ICoven Done(bool pull, PullOptions? pullOptions = null)
    {
        Board board = _registry.BuildBoard(pull, pullOptions);
        return new Coven(board);
    }
}
