// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// Adapter that turns a lambda into an <see cref="IMagikBlock{T, TOutput}"/>.
/// </summary>
public class MagikBlock<T, TOutput>(Func<T, CancellationToken, Task<TOutput>> func) : IMagikBlock<T, TOutput>
{

    /// <inheritdoc />
    public async Task<TOutput> DoMagik(T input, CancellationToken cancellationToken = default)
    {
        return await func(input, cancellationToken);
    }
}
