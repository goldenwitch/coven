// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// Entry point for running rituals: orchestrated pipelines composed of MagikBlocks.
/// </summary>
public interface ICoven
{
    /// <summary>
    /// Runs a ritual from <typeparamref name="T"/> to <typeparamref name="TOutput"/> with no initial tags.
    /// </summary>
    /// <param name="input">Input value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <typeparam name="T">Input type.</typeparam>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <returns>The ritual output.</returns>
    Task<TOutput> Ritual<T, TOutput>(T input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a ritual from <typeparamref name="T"/> to <typeparamref name="TOutput"/>, seeding initial tags that influence routing.
    /// </summary>
    /// <param name="input">Input value.</param>
    /// <param name="tags">Optional tag seed to steer selection.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <typeparam name="T">Input type.</typeparam>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <returns>The ritual output.</returns>
    Task<TOutput> Ritual<T, TOutput>(T input, List<string>? tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a ritual from <see cref="Empty"/> to <typeparamref name="TOutput"/>.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <returns>The ritual output.</returns>
    Task<TOutput> Ritual<TOutput>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts all daemons and keeps them running until <paramref name="cancellationToken"/> fires.
    /// Use for long-running, daemon-only scenarios (e.g., interactive console apps)
    /// where there is no pipeline work to run through the board.
    /// </summary>
    /// <param name="cancellationToken">Token that signals the daemons should shut down.</param>
    Task Inhabit(CancellationToken cancellationToken = default);
}
