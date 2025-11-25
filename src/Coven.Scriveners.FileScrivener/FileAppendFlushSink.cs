// SPDX-License-Identifier: BUSL-1.1

using System.Text;

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Append-only file sink for flushed snapshots.
/// Each record is serialized as a single JSON line using the configured serializer.
/// Thread-safe for a single writer; designed to be used by the flusher consumer loop.
/// </summary>
public sealed class FileAppendFlushSink<TEntry>(string path, IEntrySerializer<TEntry> serializer) : IFlushSink<TEntry>
{
    private readonly string _path = path ?? throw new ArgumentNullException(nameof(path));
    private readonly IEntrySerializer<TEntry> _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <inheritdoc />
    public async Task AppendSnapshotAsync(IReadOnlyList<(long position, TEntry entry)> snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot.Count == 0)
        {
            return;
        }

        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        StringBuilder sb = new();
        for (int i = 0; i < snapshot.Count; i++)
        {
            (long pos, TEntry entry) = snapshot[i];
            string line = _serializer.Serialize(pos, entry);
            sb.Append(line).Append('\n');
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

        using FileStream fs = new(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        await fs.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
