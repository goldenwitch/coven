// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Routing;

namespace Coven.Core.Builder;

internal interface IMagikBuilder<T, TOutput>
{
    // Allow callers to provide a custom routing strategy
    IMagikBuilder<T, TOutput> UseSelectionStrategy(ISelectionStrategy strategy);
    // Heterogeneous registration: allow adding blocks with any input/output
    IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string>? capabilities = null);

    IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, CancellationToken, Task<TOut>> func, IEnumerable<string>? capabilities = null);

    ICoven Done();
    ICoven Done(bool pull, PullOptions? pullOptions = null);
}
