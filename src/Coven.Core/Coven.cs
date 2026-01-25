// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;

namespace Coven.Core;

internal class Coven : ICoven
{
    private readonly IBoard _board;
    private readonly IServiceProvider? _rootProvider;

    internal Coven(IBoard board, IServiceProvider rootProvider)
    {
        _board = board;
        _rootProvider = rootProvider;
    }

    public async Task<TOutput> Ritual<T, TOutput>(T input, CancellationToken cancellationToken = default)
    {
        // Open DI scope (if available) for the duration of the ritual and start daemons
        DaemonScope? scope = _rootProvider is not null
            ? await CovenExecutionScope.BeginScopeAsync(_rootProvider, cancellationToken)
            : null;

        // CRITICAL: Set AsyncLocal from the synchronous context of the caller.
        // AsyncLocal modifications inside async methods don't propagate back to the caller.
        CovenExecutionScope.SetCurrentScope(scope);
        try
        {
            return await _board.PostWork<T, TOutput>(input, null, cancellationToken);
        }
        finally
        {
            CovenExecutionScope.SetCurrentScope(null);
            if (scope is not null)
            {
                await CovenExecutionScope.EndScopeAsync(scope, CancellationToken.None);
            }
        }
    }

    public async Task<TOutput> Ritual<T, TOutput>(T input, List<string>? tags, CancellationToken cancellationToken = default)
    {
        DaemonScope? scope = _rootProvider is not null
            ? await CovenExecutionScope.BeginScopeAsync(_rootProvider, cancellationToken)
            : null;

        CovenExecutionScope.SetCurrentScope(scope);
        try
        {
            return await _board.PostWork<T, TOutput>(input, tags, cancellationToken);
        }
        finally
        {
            CovenExecutionScope.SetCurrentScope(null);
            if (scope is not null)
            {
                await CovenExecutionScope.EndScopeAsync(scope, CancellationToken.None);
            }
        }
    }

    public async Task<TOutput> Ritual<TOutput>(CancellationToken cancellationToken = default)
    {
        DaemonScope? scope = _rootProvider is not null
            ? await CovenExecutionScope.BeginScopeAsync(_rootProvider, cancellationToken)
            : null;

        CovenExecutionScope.SetCurrentScope(scope);
        try
        {
            return await _board.PostWork<Empty, TOutput>(new Empty(), null, cancellationToken);
        }
        finally
        {
            CovenExecutionScope.SetCurrentScope(null);
            if (scope is not null)
            {
                await CovenExecutionScope.EndScopeAsync(scope, CancellationToken.None);
            }
        }
    }
}
