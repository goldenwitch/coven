// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Console;

/// <summary>
/// Abstraction over console I/O streams to enable testing without actual console.
/// </summary>
/// <remarks>
/// <para>
/// Production code uses <see cref="SystemConsoleIO"/> which wraps <see cref="System.Console"/>.
/// Test code can provide a virtual implementation that uses in-memory streams or channels.
/// </para>
/// </remarks>
public interface IConsoleIO
{
    /// <summary>
    /// Gets the standard input stream.
    /// </summary>
    TextReader Input { get; }

    /// <summary>
    /// Gets the standard output stream.
    /// </summary>
    TextWriter Output { get; }

    /// <summary>
    /// Gets the standard error stream.
    /// </summary>
    TextWriter ErrorOutput { get; }

    /// <summary>
    /// Reads a line asynchronously from the input stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The line read, or <c>null</c> if end of stream.</returns>
    ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes a line asynchronously to the output stream.
    /// </summary>
    /// <param name="value">The text to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask WriteLineAsync(string value, CancellationToken cancellationToken);
}
