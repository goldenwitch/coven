// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Determines when a buffered stream window should emit one or more outputs.
/// </summary>
/// <typeparam name="TChunk">The chunk type being windowed.</typeparam>
public interface IWindowPolicy<TChunk>
{
    /// <summary>
    /// The minimum number of recent chunks required to evaluate <see cref="ShouldEmit"/>.
    /// </summary>
    int MinChunkLookback { get; }

    /// <summary>
    /// Returns true when the current window should emit.
    /// </summary>
    /// <param name="window">The current stream window context.</param>
    /// <returns><c>true</c> to emit; otherwise <c>false</c>.</returns>
    bool ShouldEmit(StreamWindow<TChunk> window);
}
