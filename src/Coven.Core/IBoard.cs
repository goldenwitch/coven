// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// Internal orchestration board that schedules and executes work across MagikBlocks.
/// </summary>
public interface IBoard
{
    /// <summary>
    /// Posts work to the board and awaits the resulting output.
    /// </summary>
    /// <typeparam name="T">Input type.</typeparam>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <param name="input">Input value.</param>
    /// <param name="tags">Optional tags that influence selection.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The computed output.</returns>
    Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a unit of work for a worker and delivers results via the provided sink.
    /// </summary>
    /// <typeparam name="TIn">Input type for the requested work.</typeparam>
    /// <param name="request">Work request options.</param>
    /// <param name="sink">Orchestrator callbacks for result delivery.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task GetWork<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when a block exists that can handle the given input type and tags.
    /// </summary>
    /// <typeparam name="T">Input type.</typeparam>
    /// <param name="tags">Candidate tag set.</param>
    /// <returns><c>true</c> if work is supported; otherwise <c>false</c>.</returns>
    bool WorkSupported<T>(List<string> tags);
}
