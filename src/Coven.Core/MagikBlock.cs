// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

public class MagikBlock<T, TOutput>(Func<T, CancellationToken, Task<TOutput>> func) : IMagikBlock<T, TOutput>
{

    public async Task<TOutput> DoMagik(T input, CancellationToken cancellationToken = default)
    {
        return await func(input, cancellationToken);
    }
}
