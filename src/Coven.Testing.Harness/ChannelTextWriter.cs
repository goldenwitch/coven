// SPDX-License-Identifier: BUSL-1.1

using System.Text;
using System.Threading.Channels;

namespace Coven.Testing.Harness;

/// <summary>
/// A TextWriter that writes lines to a Channel for test capture.
/// Buffers partial writes and flushes complete lines.
/// </summary>
internal sealed class ChannelTextWriter(ChannelWriter<string> writer) : TextWriter
{
    private readonly ChannelWriter<string> _writer = writer;
    private readonly StringBuilder _buffer = new();

    /// <summary>
    /// Gets the character encoding in which the output is written (UTF-8).
    /// </summary>
    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>
    /// Writes a character to the buffer. Flushes on newline.
    /// </summary>
    /// <param name="value">The character to write.</param>
    public override void Write(char value)
    {
        if (value == '\n')
        {
            Flush();
        }
        else if (value != '\r')
        {
            _buffer.Append(value);
        }
    }

    /// <summary>
    /// Writes a string followed by a line terminator to the channel.
    /// </summary>
    /// <param name="value">The string to write (may be null).</param>
    public override void WriteLine(string? value)
    {
        _buffer.Append(value);
        Flush();
    }

    /// <summary>
    /// Flushes the current buffer content to the channel as a single line.
    /// </summary>
    public override void Flush()
    {
        if (_buffer.Length > 0)
        {
            _writer.TryWrite(_buffer.ToString());
            _buffer.Clear();
        }
    }

    /// <summary>
    /// Asynchronously writes a string followed by a line terminator to the channel.
    /// </summary>
    /// <param name="value">The string to write (may be null).</param>
    public override async Task WriteLineAsync(string? value)
    {
        string line = _buffer.Append(value).ToString();
        _buffer.Clear();
        await _writer.WriteAsync(line).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes a string followed by a line terminator to the channel.
    /// </summary>
    /// <param name="value">The string to write (may be null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public override async Task WriteLineAsync(ReadOnlyMemory<char> value, CancellationToken cancellationToken = default)
    {
        _buffer.Append(value);
        string line = _buffer.ToString();
        _buffer.Clear();
        await _writer.WriteAsync(line, cancellationToken).ConfigureAwait(false);
    }
}
