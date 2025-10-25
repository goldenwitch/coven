// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

public sealed class LambdaShatterPolicy<TSource, TChunk>(Func<TSource, IEnumerable<TChunk>> shatter)
    : IShatterPolicy<TSource, TChunk>
{
    private readonly Func<TSource, IEnumerable<TChunk>> _shatter = shatter ?? throw new ArgumentNullException(nameof(shatter));

    public IEnumerable<TChunk> Shatter(TSource source)
        => _shatter(source);
}

