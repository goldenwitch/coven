// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public class MagikBlock<T, TOutput> : IMagikBlock<T, TOutput>
{
    private readonly Func<T, Task<TOutput>> Magik;
    public MagikBlock(Func<T, Task<TOutput>> func)
    {
        Magik = func;
    }

    public async Task<TOutput> DoMagik(T input)
    {
        return await Magik(input);
    }
}