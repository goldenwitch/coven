// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Di;

namespace Coven.Core;

internal class Coven : ICoven
{
    private readonly IBoard board;
    private readonly IServiceProvider? rootProvider;

    internal Coven(IBoard board)
    {
        this.board = board;
    }

    internal Coven(IBoard board, IServiceProvider rootProvider)
    {
        this.board = board;
        this.rootProvider = rootProvider;
    }

    public async Task<TOutput> Ritual<T, TOutput>(T input)
    {
        // Ask the board if it's okay to post
        if (!board.WorkSupported<T>(new List<string>()))
        {
            throw new InvalidOperationException("Board does not support this work.");
        }

        // Open DI scope (if available) for the duration of the ritual
        var scope = rootProvider is not null ? CovenExecutionScope.BeginScope(rootProvider) : null;
        try
        {
            return await board.PostWork<T, TOutput>(input).ConfigureAwait(false);
        }
        finally
        {
            if (rootProvider is not null) CovenExecutionScope.EndScope(scope);
        }
    }

    public async Task<TOutput> Ritual<T, TOutput>(T input, List<string>? tags)
    {
        // Ask the board if it's okay to post with tags
        if (!board.WorkSupported<T>(tags ?? new List<string>()))
        {
            throw new InvalidOperationException("Board does not support this work.");
        }

        var scope = rootProvider is not null ? CovenExecutionScope.BeginScope(rootProvider) : null;
        try
        {
            return await board.PostWork<T, TOutput>(input, tags).ConfigureAwait(false);
        }
        finally
        {
            if (rootProvider is not null) CovenExecutionScope.EndScope(scope);
        }
    }

    public async Task<TOutput> Ritual<TOutput>()
    {
        // Ask the board if it's okay to post with no input
        if (!board.WorkSupported<Empty>(new List<string>()))
        {
            throw new InvalidOperationException("Board does not support this work.");
        }

        var scope = rootProvider is not null ? CovenExecutionScope.BeginScope(rootProvider) : null;
        try
        {
            return await board.PostWork<Empty, TOutput>(Empty.Value).ConfigureAwait(false);
        }
        finally
        {
            if (rootProvider is not null) CovenExecutionScope.EndScope(scope);
        }
    }
}