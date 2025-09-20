// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Routing;

namespace Coven.Core.Builder;

internal class MagikBlockRegistration<T, TOutput> : IMagikBuilder<T, TOutput>
{
    private readonly IMagikBuilder<T, TOutput> _magikBuilder;

    internal MagikBlockRegistration(IMagikBuilder<T, TOutput> magikBuilder)
    {
        _magikBuilder = magikBuilder;
    }

    public IMagikBuilder<T, TOutput> UseSelectionStrategy(ISelectionStrategy strategy)
    {
        _magikBuilder.UseSelectionStrategy(strategy);
        return this;
    }

    public ICoven Done()
    {
        return _magikBuilder.Done();
    }

    public ICoven Done(bool pull, PullOptions? pullOptions)
    {
        return _magikBuilder.Done(pull, pullOptions);
    }

    // Heterogeneous registration passthroughs (with optional capabilities)
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string>? capabilities = null)
    {
        return _magikBuilder.MagikBlock(block, capabilities);
    }

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, CancellationToken, Task<TOut>> func, IEnumerable<string>? capabilities = null)
    {
        return _magikBuilder.MagikBlock(func, capabilities);
    }

}
