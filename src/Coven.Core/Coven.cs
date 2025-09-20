// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core;

internal class Coven : ICoven
{
    private readonly IBoard _board;
    private readonly IServiceProvider? _rootProvider;

    internal Coven(IBoard board)
    {
        _board = board;
    }

    internal Coven(IBoard board, IServiceProvider rootProvider)
    {
        _board = board;
        _rootProvider = rootProvider;
    }

    public async Task<TOutput> Ritual<T, TOutput>(T input, CancellationToken cancellationToken = default)
    {
        // Ask the board if it's okay to post
        if (!_board.WorkSupported<T>([]))
        {
            throw new InvalidOperationException("Board does not support this work.");
        }

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
        // Ask the board if it's okay to post with tags
        if (!_board.WorkSupported<T>(tags ?? []))
        {
            throw new InvalidOperationException("Board does not support this work.");
        }

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
        // Ask the board if it's okay to post with no input
        if (!_board.WorkSupported<Empty>([]))
        {
            throw new InvalidOperationException("Board does not support this work.");
        }

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
