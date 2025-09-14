// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Tail;

/// <summary>
/// Read-only tail source that follows a text file from the beginning,
/// emitting lines as TailEvents and continuing to watch for appended content.
/// </summary>
public sealed class DocumentTailSource : ITailSource, IAsyncDisposable
{
    private readonly string _path;
    private readonly TimeSpan _pollInterval;
    private volatile bool _disposed;

    public DocumentTailSource(string path, TimeSpan? pollInterval = null)
    {
        _path = path;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(150);
    }

    public async Task TailAsync(Func<TailEvent, ValueTask> onMessage, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        // Wait for file to exist
        while (!File.Exists(_path))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
        }

        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);

        // Read existing content first
        string? line;
        while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            await onMessage(new Line(line, DateTimeOffset.UtcNow)).ConfigureAwait(false);
        }

        // Tail appended content
        while (!ct.IsCancellationRequested)
        {
            line = await sr.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                try { await Task.Delay(_pollInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await onMessage(new Line(line, DateTimeOffset.UtcNow)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await onMessage(new ErrorLine(ex.Message, DateTimeOffset.UtcNow)).ConfigureAwait(false);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DocumentTailSource));
    }
}
