// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Channels;

namespace Coven.Testing.Harness;

/// <summary>
/// A TextReader that reads lines from a Channel. Returns null from ReadLineAsync
/// when the channel completes, signaling EOF to consumers like stdin pumps.
/// </summary>
internal sealed class ChannelTextReader(ChannelReader<string> reader) : TextReader
{
    private readonly ChannelReader<string> _reader = reader;

    /// <summary>
    /// Attempts to synchronously read a line from the channel.
    /// </summary>
    /// <returns>The line read, or <c>null</c> if no data is immediately available.</returns>
    public override string? ReadLine()
    {
        return _reader.TryRead(out string? line) ? line : null;
    }

    /// <summary>
    /// Asynchronously reads a line from the channel.
    /// </summary>
    /// <returns>The line read, or <c>null</c> if the channel has completed (EOF).</returns>
    public override Task<string?> ReadLineAsync()
    {
        return ReadLineAsyncCore(CancellationToken.None).AsTask();
    }

    /// <summary>
    /// Asynchronously reads a line from the channel with cancellation support.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The line read, or <c>null</c> if the channel has completed (EOF).</returns>
    public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        return ReadLineAsyncCore(cancellationToken);
    }

    private async ValueTask<string?> ReadLineAsyncCore(CancellationToken cancellationToken)
    {
        try
        {
            // WaitToReadAsync returns false when Complete() has been called
            // and all buffered items are consumed
            if (await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_reader.TryRead(out string? line))
                {
                    return line;
                }
            }
            // Channel completed = EOF
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }
}
