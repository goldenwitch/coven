using System.Collections.Generic;

namespace Coven.Core;

internal class Coven : ICoven
{
    private readonly IBoard board;

    internal Coven(IBoard board)
    {
        this.board = board;
    }

    public async Task<TOutput> Ritual<T, TOutput>(T input)
    {
        // Ask the board if it's okay to post
        if (!board.WorkSupported<T>(new List<string>()))
        {
            throw new InvalidOperationException("Board does not support this work.");
        }

        // Next we post the input to the board.
        return await board.PostWork<T, TOutput>(input);
    }
}
