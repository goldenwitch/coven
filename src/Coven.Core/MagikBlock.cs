// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public class MagikBlock<T, TOutput> : IMagikBlock<T, TOutput>
{
    private readonly Func<T, CancellationToken, Task<TOutput>> Magik;
    public MagikBlock(Func<T, CancellationToken, Task<TOutput>> func)
    {
        Magik = func;
    }

    public async Task<TOutput> DoMagik(T input, CancellationToken cancellationToken = default)
    {
        return await Magik(input, cancellationToken);
    }
}
