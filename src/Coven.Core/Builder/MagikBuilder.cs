// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Routing;

namespace Coven.Core.Builder;

public class MagikBuilder<T, TOutput> : IMagikBuilder<T, TOutput>
{
    private readonly List<MagikBlockDescriptor> registry = new();
    private ISelectionStrategy? selectionStrategy;

    public IMagikBuilder<T, TOutput> UseSelectionStrategy(ISelectionStrategy strategy)
    {
        selectionStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        return this;
    }

    // Heterogeneous registration (with optional capabilities)
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string>? capabilities = null)
    {
        registry.Add(new MagikBlockDescriptor(
            typeof(TIn),
            typeof(TOut),
            block,
            capabilities?.ToList(),
            block.GetType().Name));
        return this;
    }

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, CancellationToken, Task<TOut>> func, IEnumerable<string>? capabilities = null)
    {
        var mb = new MagikBlock<TIn, TOut>(func);
        registry.Add(new MagikBlockDescriptor(
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
        var mode = pull ? Board.BoardMode.Pull : Board.BoardMode.Push;
        // Always precompile pipelines at Done() time for consistent performance
        var board = new Board(mode, registry.AsReadOnly(), pullOptions: pullOptions, selectionStrategy: selectionStrategy);
        return new Coven(board);
    }
}
