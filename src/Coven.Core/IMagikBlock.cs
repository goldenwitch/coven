// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// Processing unit that transforms <typeparamref name="T"/> into <typeparamref name="TOutput"/>.
/// Implementations should be stateless or treat state as ephemeral per invocation.
/// </summary>
public interface IMagikBlock<T, TOutput>
{
    /// <summary>
    /// Performs the work of the block.
    /// </summary>
    /// <param name="input">Input value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The output value.</returns>
    Task<TOutput> DoMagik(T input, CancellationToken cancellationToken = default);
}
