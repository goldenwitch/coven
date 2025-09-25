// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;

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
        // Open DI scope (if available) for the duration of the ritual
        IServiceScope? scope = _rootProvider is not null ? CovenExecutionScope.BeginScope(_rootProvider) : null;
        try
        {
            return await _board.PostWork<T, TOutput>(input, null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (_rootProvider is not null)
            {
                CovenExecutionScope.EndScope(scope);
            }

        }
    }

    public async Task<TOutput> Ritual<T, TOutput>(T input, List<string>? tags, CancellationToken cancellationToken = default)
    {
        IServiceScope? scope = _rootProvider is not null ? CovenExecutionScope.BeginScope(_rootProvider) : null;
        try
        {
            return await _board.PostWork<T, TOutput>(input, tags, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (_rootProvider is not null)
            {
                CovenExecutionScope.EndScope(scope);
            }

        }
    }

    public async Task<TOutput> Ritual<TOutput>(CancellationToken cancellationToken = default)
    {
        IServiceScope? scope = _rootProvider is not null ? CovenExecutionScope.BeginScope(_rootProvider) : null;
        try
        {
            return await _board.PostWork<Empty, TOutput>(new Empty(), null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (_rootProvider is not null)
            {
                CovenExecutionScope.EndScope(scope);
            }

        }
    }
}
