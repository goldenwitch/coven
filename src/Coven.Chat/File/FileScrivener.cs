using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coven.Chat;

/// <summary>
/// File-backed implementation of IScrivener&lt;T&gt; using a directory of JSON files, one per record.
/// - Positions are the zero-padded file name (e.g., 0000000000000001.json) for lexicographic order.
/// - Writes are serialized via a process-wide file lock (lock file opened with FileShare.None).
/// - Reads tail by checking the next expected file and awaiting filesystem notifications.
/// - Enforces forward-read contiguity: readers never leap past missing/unreadable earlier positions.
/// </summary>
public sealed class FileScrivener<TJournalEntryType> : IScrivener<TJournalEntryType> where TJournalEntryType : notnull
{
    private readonly string _root;
    private readonly string _lockPath;
    private readonly string _headPath;
    private readonly JsonSerializerOptions _json;
    private readonly ILogger<FileScrivener<TJournalEntryType>> _log;

    private const string Extension = ".json";
    private const int NameDigits = 20; // supports up to 9,223,372,036,854,775,807

    // Position allocator (last assigned), persisted in head.txt; only mutated under write lock
    private long _lastAssigned;

    // Watcher + async signal (shared across waiters)
    private TaskCompletionSource<bool> _signal = NewSignal();
    private FileSystemWatcher? _watcher;

    public FileScrivener(string rootDirectory, JsonSerializerOptions? json = null)
        : this(rootDirectory, json, NullLogger<FileScrivener<TJournalEntryType>>.Instance)
    { }

    public FileScrivener(string rootDirectory, JsonSerializerOptions? json, ILogger<FileScrivener<TJournalEntryType>> logger)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Required", nameof(rootDirectory));
        _root = rootDirectory;
        Directory.CreateDirectory(_root);
        _lockPath = Path.Combine(_root, "journal.lock");
        _headPath = Path.Combine(_root, "head.txt");

        _json = json ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _log = logger ?? NullLogger<FileScrivener<TJournalEntryType>>.Instance;

        // Bootstrap: try head.txt; if missing, compute max from filenames once.
        _lastAssigned = TryReadHead(_headPath);
        if (_lastAssigned == 0)
        {
            long max = 0;
            foreach (var path in Directory.EnumerateFiles(_root, "*" + Extension, SearchOption.TopDirectoryOnly))
            {
                if (TryParsePosition(Path.GetFileNameWithoutExtension(path), out var pos) && pos > max)
                    max = pos;
            }
            _lastAssigned = max; // next write will allocate max+1
        }

        EnsureWatcherInitialized();
    }

    public async Task<long> WriteAsync(TJournalEntryType entry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (entry is null) throw new ArgumentNullException(nameof(entry));

        await using var guard = await AcquireWriteLockAsync(ct).ConfigureAwait(false);

        // Clean up any orphaned temp files (best-effort)
        foreach (var tmp in Directory.EnumerateFiles(_root, "*" + Extension + ".tmp", SearchOption.TopDirectoryOnly))
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }

        // Find next available candidate position under the lock.
        long candidate;
        checked { candidate = _lastAssigned + 1; }
        while (File.Exists(Path.Combine(_root, PositionToName(candidate) + Extension)) ||
               File.Exists(Path.Combine(_root, PositionToName(candidate) + Extension + ".tmp")))
        {
            checked { candidate++; }
        }

        // Attempt write → move. If destination appears between check and move, advance and retry.
        while (true)
        {
            var fileName = PositionToName(candidate) + Extension;
            var tmpPath = Path.Combine(_root, fileName + ".tmp");
            var finalPath = Path.Combine(_root, fileName);

            await using (var fs = new FileStream(
                tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var wrapper = new RecordWrapper
                {
                    Pos = candidate,
                    Type = entry.GetType().AssemblyQualifiedName!,
                    Payload = entry
                };
            await JsonSerializer.SerializeAsync(fs, wrapper, _json, ct).ConfigureAwait(false);
            await fs.FlushAsync(ct).ConfigureAwait(false);
            try { fs.Flush(true); } catch (NotSupportedException) { /* best-effort */ }
            }

            try
            {
                File.Move(tmpPath, finalPath);
                _lastAssigned = candidate;
                PersistHead(_headPath, candidate); // best-effort
                TryFlushDirectory(_root);          // best-effort
                _log.LogDebug("chat:file write pos={Pos} path={Path}", candidate, finalPath);
                return candidate;
            }
            catch (IOException)
            {
                try { File.Delete(tmpPath); } catch { /* ignore */ }
                checked { candidate++; }
                _log.LogTrace("chat:file collision advance next={Pos}", candidate);
            }
        }
    }

    public async IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> TailAsync(
        long afterPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (afterPosition == long.MaxValue)
            yield break; // nothing is after MaxValue

        long next = afterPosition + 1;
        _log.LogTrace("chat:file tail start after={After}", afterPosition);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var path = Path.Combine(_root, PositionToName(next) + Extension);
            if (File.Exists(path))
            {
                var entry = TryRead(path);
                if (entry is not null)
                {
                    yield return (next, entry);
                    _log.LogTrace("chat:file tail yield pos={Pos}", next);
                    next++;
                    continue;
                }

                // File exists but is unreadable: enforce contiguity (do not skip)
                _log.LogTrace("chat:file tail unreadable wait pos={Pos}", next);
                await WaitForNewFileAsync(ct).ConfigureAwait(false);
                continue;
            }

            // Next file not present yet; wait for filesystem activity
            _log.LogTrace("chat:file tail wait pos={Pos}", next);
            await WaitForNewFileAsync(ct).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> ReadBackwardAsync(
        long beforePosition = long.MaxValue,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Snapshot the directory and iterate by position descending
        var files = Directory.EnumerateFiles(_root, "*" + Extension, SearchOption.TopDirectoryOnly)
                             .Select(p => (p, name: Path.GetFileNameWithoutExtension(p)))
                             .Where(t => TryParsePosition(t.name, out _))
                             .Select(t => (pos: long.Parse(t.name, CultureInfo.InvariantCulture), path: t.p))
                             .Where(t => t.pos < beforePosition)
                             .OrderByDescending(t => t.pos)
                             .ToList();

        await Task.Yield();
        
        foreach (var (pos, path) in files)
        {
            ct.ThrowIfCancellationRequested();
            var entry = TryRead(path);
            if (entry is null) continue; // backward read may skip unreadable (policy)
            yield return (pos, entry);
            _log.LogTrace("chat:file back yield pos={Pos}", pos);
        }
    }

    public async Task<(long journalPosition, TJournalEntryType entry)> WaitForAsync(
        long afterPosition,
        Func<TJournalEntryType, bool> match,
        CancellationToken ct = default)
    {
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (afterPosition == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(afterPosition));

        long next = afterPosition + 1;
        _log.LogTrace("chat:file wait start after={After}", afterPosition);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var path = Path.Combine(_root, PositionToName(next) + Extension);
            if (File.Exists(path))
            {
                var entry = TryRead(path);
                if (entry is not null)
                {
                    if (match(entry))
                    {
                        _log.LogDebug("chat:file wait match pos={Pos}", next);
                        return (next, entry);
                    }

                    next++;
                    continue;
                }

                // Exists but unreadable => enforce contiguity
                _log.LogTrace("chat:file wait unreadable pos={Pos}", next);
                await WaitForNewFileAsync(ct).ConfigureAwait(false);
                continue;
            }

            _log.LogTrace("chat:file wait sleeping next={Next}", next);
            await WaitForNewFileAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(
        long afterPosition,
        CancellationToken ct = default) where TDerived : TJournalEntryType
    {
        var (pos, e) = await WaitForAsync(afterPosition, static e => e is TDerived, ct).ConfigureAwait(false);
        return (pos, (TDerived)e);
    }

    // --- Helpers ---

    private async Task<IAsyncDisposable> AcquireWriteLockAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fs = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new AsyncDisposer(fs);
            }
            catch (IOException)
            {
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }
    }

    private sealed class AsyncDisposer : IAsyncDisposable
    {
        private readonly FileStream _fs;
        public AsyncDisposer(FileStream fs) { _fs = fs; }
        public ValueTask DisposeAsync() { _fs.Dispose(); return ValueTask.CompletedTask; }
    }

    private static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    private void EnsureWatcherInitialized()
    {
        if (_watcher is not null) return;

        var w = new FileSystemWatcher(_root)
        {
            Filter = "*" + Extension,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            IncludeSubdirectories = false,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = false
        };

        void Bump() => _signal.TrySetResult(true);

        // Attach handlers BEFORE enabling
        w.Created += (_, __) => Bump();
        w.Renamed += (_, __) => Bump();
        w.Changed += (_, __) => Bump();
        w.Error   += (_, __) => Bump();

        w.EnableRaisingEvents = true;
        _watcher = w;
    }
    private async Task WaitForNewFileAsync(CancellationToken ct)
    {
        // Capture the current signal and race with a short delay (poll-backoff)
        var waiter = Volatile.Read(ref _signal);
        var delay = Task.Delay(250, ct);
        var completed = await Task.WhenAny(waiter.Task, delay).ConfigureAwait(false);

        // Rotate the signal only if the watcher fired (avoid consuming future events)
        if (completed == waiter.Task)
            Interlocked.Exchange(ref _signal, NewSignal());

        // Re-assert cancellation to surface timely cancellation to callers
        ct.ThrowIfCancellationRequested();
    }

    private TJournalEntryType? TryRead(string path)
    {
        try
        {
            using var fs = new FileStream(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);

            var wrapper = JsonSerializer.Deserialize<RecordWrapper>(fs, _json);
            if (wrapper is null || string.IsNullOrWhiteSpace(wrapper.Type)) return default;

            var type = Type.GetType(wrapper.Type, throwOnError: false);
            if (type is null || !typeof(TJournalEntryType).IsAssignableFrom(type))
                return default;

            if (wrapper.Payload is JsonElement je)
                return (TJournalEntryType?)je.Deserialize(type, _json);

            return (TJournalEntryType?)wrapper.Payload;
        }
        catch
        {
            return default;
        }
    }

    private static bool TryParsePosition(string name, out long pos)
        => long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out pos);

    private static string PositionToName(long pos)
        => pos.ToString($"D{NameDigits}", CultureInfo.InvariantCulture);

    private static long TryReadHead(string path)
    {
        try
        {
            if (!File.Exists(path)) return 0;
            var s = File.ReadAllText(path);
            return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
        catch { return 0; }
    }

    private static void PersistHead(string path, long value)
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, value.ToString(CultureInfo.InvariantCulture));
            // Prefer overwrite move; if not available/supported on platform, fall back silently.
            try { File.Move(tmp, path, overwrite: true); }
            catch { try { File.Replace(tmp, path, null); } catch { /* ignore */ } }
        }
        catch { /* best-effort */ }
    }

    private static void TryFlushDirectory(string dir)
    {
        try
        {
            // Best-effort: on .NET 8+ this opens a handle to the directory and flushes it to disk.
            // If the platform/filesystem does not support directory handles, fall back silently.
            using var handle = File.OpenHandle(
                dir,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                FileOptions.None);
            RandomAccess.FlushToDisk(handle);
        }
        catch { /* best-effort */ }
    }
}
