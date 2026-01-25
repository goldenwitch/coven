// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Channels;
using Coven.Chat.Console;

namespace Coven.Testing.Harness;

/// <summary>
/// Virtual console I/O implementation for E2E testing. Provides channel-based
/// input/output streams that can be controlled by test code.
/// </summary>
public sealed class VirtualConsoleIO : IConsoleIO, IAsyncDisposable
{
    private readonly Channel<string> _inputChannel = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _outputChannel = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _errorChannel = Channel.CreateUnbounded<string>();

    /// <summary>
    /// Creates a new VirtualConsoleIO instance with unbounded channels.
    /// </summary>
    public VirtualConsoleIO()
    {
        Input = new ChannelTextReader(_inputChannel.Reader);
        Output = new ChannelTextWriter(_outputChannel.Writer);
        ErrorOutput = new ChannelTextWriter(_errorChannel.Writer);
    }

    /// <inheritdoc />
    public TextReader Input { get; }

    /// <inheritdoc />
    public TextWriter Output { get; }

    /// <inheritdoc />
    public TextWriter ErrorOutput { get; }

    /// <inheritdoc />
    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await _inputChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_inputChannel.Reader.TryRead(out string? line))
                {
                    return line;
                }
            }
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask WriteLineAsync(string value, CancellationToken cancellationToken)
    {
        return _outputChannel.Writer.WriteAsync(value, cancellationToken);
    }

    // === Test Input API ===

    /// <summary>
    /// Sends a line of input to the console. The line becomes available
    /// to the stdin pump's ReadLineAsync call.
    /// </summary>
    /// <param name="line">The line to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask SendInputAsync(string line, CancellationToken cancellationToken = default)
    {
        return _inputChannel.Writer.WriteAsync(line, cancellationToken);
    }

    /// <summary>
    /// Signals EOF on stdin. After this call, ReadLineAsync will return null,
    /// causing stdin pumps to break out of their loops cooperatively.
    /// </summary>
    public void CompleteInput()
    {
        _inputChannel.Writer.Complete();
    }

    // === Test Output API ===

    /// <summary>
    /// Waits for and returns the next line written to stdout.
    /// Throws TimeoutException if no output arrives within the timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next output line.</returns>
    /// <exception cref="TimeoutException">No output received within timeout.</exception>
    public async ValueTask<string> WaitForOutputAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await _outputChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No output received within {timeout}");
        }
    }

    /// <summary>
    /// Collects exactly <paramref name="count"/> output lines.
    /// </summary>
    /// <param name="count">Number of lines to collect.</param>
    /// <param name="timeout">Maximum time to wait for all lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collected output lines.</returns>
    /// <exception cref="OperationCanceledException">Timeout or cancellation occurred.</exception>
    public async ValueTask<IReadOnlyList<string>> CollectOutputAsync(
        int count, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        List<string> lines = new(count);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        for (int i = 0; i < count; i++)
        {
            lines.Add(await _outputChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false));
        }

        return lines;
    }

    /// <summary>
    /// Collects all output lines that arrive within the specified timeout.
    /// Returns when the timeout elapses or the channel completes.
    /// </summary>
    /// <param name="timeout">Time to wait for additional output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All collected output lines.</returns>
    public async ValueTask<IReadOnlyList<string>> CollectAllOutputAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        List<string> lines = [];
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            while (await _outputChannel.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
            {
                while (_outputChannel.Reader.TryRead(out string? line))
                {
                    lines.Add(line);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout reached, return what we have
        }

        return lines;
    }

    /// <summary>
    /// Drains all currently buffered output without waiting.
    /// </summary>
    /// <returns>All currently buffered output lines.</returns>
    public IReadOnlyList<string> DrainOutput()
    {
        List<string> lines = [];
        while (_outputChannel.Reader.TryRead(out string? line))
        {
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Drains all currently buffered error output without waiting.
    /// </summary>
    /// <returns>All currently buffered error lines.</returns>
    public IReadOnlyList<string> DrainErrorOutput()
    {
        List<string> lines = [];
        while (_errorChannel.Reader.TryRead(out string? line))
        {
            lines.Add(line);
        }
        return lines;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _inputChannel.Writer.TryComplete();
        _outputChannel.Writer.TryComplete();
        _errorChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
