# Coven.Scriveners.FileScrivener

File-backed `IScrivener<T>` with a snapshot flusher daemon. In‑process journaling remains fully in‑memory and replayable; a background daemon tails the journal and appends newline‑delimited snapshots to disk.

## What It Provides

- `FileScrivener<TEntry>`: a wrapper over `InMemoryScrivener<TEntry>` that performs no I/O and exposes the standard `IScrivener<TEntry>` API.
- `FlusherDaemon<TEntry>`: tails the journal, accumulates an ordered snapshot of `(position, entry)` pairs, and flushes to a sink when a predicate is met; flushes any remainder on shutdown.
- Defaults: JSON serializer (`{ schemaVersion: string, position, entry }` per line), append‑only file sink, and a count‑based flush predicate.

## Install / DI

```csharp
using Coven.Core;
using Coven.Core.Daemonology;
using Coven.Scriveners.FileScrivener;
using Microsoft.Extensions.DependencyInjection;

services.AddFileScrivener<MyEntry>(new FileScrivenerConfig
{
    FilePath = "./journal.ndjson",   // destination file (directories auto-created)
    FlushThreshold = 100,              // default predicate: flush every 100 entries
    FlushQueueCapacity = 8             // bounded ring buffer for snapshots
});

// FlusherDaemon<TEntry> is registered as a ContractDaemon — start it in your block.
```

What `AddFileScrivener<TEntry>` registers:
- `IScrivener<TEntry>` → `FileScrivener<TEntry>` backed by a keyed `InMemoryScrivener<TEntry>`.
- `IEntrySerializer<TEntry>` → `JsonEntrySerializer<TEntry>` (TryAdd; overrideable).
- `IFlushSink<TEntry>` → `FileAppendFlushSink<TEntry>` (TryAdd; overrideable).
- `IFlushPredicate<TEntry>` → `CountThresholdFlushPredicate<TEntry>` using `FlushThreshold` (TryAdd; overrideable).
- `IScrivener<DaemonEvent>` → `InMemoryScrivener<DaemonEvent>` for daemon status events.
- `ContractDaemon` → `FlusherDaemon<TEntry>`.

## How It Works

Producer (tail):
- Tails `_scrivener.TailAsync(0, ct)` and appends `(position, entry)` pairs to a producer‑owned `_activeSnapshot` list.
- When `IFlushPredicate<TEntry>.ShouldFlush(snapshot)` returns `true`, it swaps the active buffer with a fresh one (rented from a bounded pool) and enqueues the full snapshot to a bounded flush queue.
- On cancellation/completion, enqueues any remaining snapshot and completes the queue.

Consumer (flush):
- Single reader of the bounded flush queue; for each snapshot batch:
  - Serializes each entry line via `IEntrySerializer<TEntry>` and appends to file (`FileAppendFlushSink<TEntry>` uses UTF‑8 NDJSON, `FileShare.Read`).
  - Clears the list and returns it to the pool for reuse.

Threading & safety:
- Single producer/consumer tasks; snapshots are persisted atomically per batch in arrival order.
- Directories are created if needed. File writes are append‑only and flushed.
- Remaining in‑memory data is flushed on shutdown. Failures are surfaced via the daemon’s status journal.

Important: File persistence is append‑only; reading/recovery from disk is not implemented — replay comes from the in‑memory scrivener for the current process. If you need recovery, implement a startup loader that hydrates the inner scrivener from the file.

Compatibility: The on‑disk format intentionally does not commit to backward/forward compatibility. The file scrivener makes no promises about reading any version other than the one it wrote. Each line includes a `schemaVersion` field to enable readers to detect and reject incompatible data.

## Start the Daemon

Start `ContractDaemon`s from your block (pattern used across the repo):

```csharp
using Coven.Core;
using Coven.Core.Daemonology;

internal sealed class StartDaemonsBlock(IEnumerable<ContractDaemon> daemons) : IMagikBlock<Empty, Empty>
{
    private readonly IEnumerable<ContractDaemon> _daemons = daemons ?? throw new ArgumentNullException(nameof(daemons));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        foreach (ContractDaemon d in _daemons)
        {
            await d.Start(cancellationToken);
            // Optionally: await d.WaitFor(Status.Running, cancellationToken);
        }
        return input;
    }
}
```

## Customization

- Serializer (line format):
  - Default is `JsonEntrySerializer<TEntry>` producing `{ schemaVersion: string, position, entry }` per line.
    - The `entry` object is serialized polymorphically with System.Text.Json using type discriminators (annotated on entry base types).
  - Replace by registering your own:
    ```csharp
    services.AddScoped<IEntrySerializer<MyEntry>, MyCustomSerializer>();
    ```

- Flush predicate (when to persist):
  - Default: `CountThresholdFlushPredicate<TEntry>` using `FlushThreshold`.
  - Provide time‑based or size‑based logic:
    ```csharp
    public sealed class TimeOrCountPredicate<T> : IFlushPredicate<T>
    {
        private readonly int _count;
        private readonly TimeProvider _clock;
        private DateTimeOffset _last = DateTimeOffset.UtcNow;
        public TimeOrCountPredicate(int count, TimeProvider? clock = null)
        { _count = count; _clock = clock ?? TimeProvider.System; }
        public bool ShouldFlush(IReadOnlyList<(long position, T entry)> snapshot)
        {
            if (snapshot.Count >= _count) { _last = _clock.GetUtcNow(); return true; }
            if (_clock.GetUtcNow() - _last >= TimeSpan.FromSeconds(2)) { _last = _clock.GetUtcNow(); return snapshot.Count > 0; }
            return false;
        }
    }
    services.AddScoped<IFlushPredicate<MyEntry>>(_ => new TimeOrCountPredicate<MyEntry>(50));
    ```

- Sink (where to persist):
  - Default: `FileAppendFlushSink<TEntry>` with async append and `FileShare.Read`.
  - Swap for rotation/cloud/etc.:
    ```csharp
    services.AddScoped<IFlushSink<MyEntry>, RotatingFileSink<MyEntry>>();
    ```

Note: Defaults are registered with `TryAdd`. You can register your own serializer/predicate/sink before or after `AddFileScrivener<TEntry>`; the last registration wins for single service resolution.

## Configuration

`FileScrivenerConfig`:
- `FilePath`: destination file path (directories auto‑created).
- `FlushThreshold`: default count predicate threshold (>= 1, default 100).
- `FlushQueueCapacity`: bounded snapshot queue capacity (default 8). Pool size is `FlushQueueCapacity + 2`.

## Example End‑to‑End

```csharp
// 1) Register journaling for a specific entry type
services.AddFileScrivener<MyEntry>(new FileScrivenerConfig
{
    FilePath = "./data/my-entry.ndjson",
    FlushThreshold = 200,
    FlushQueueCapacity = 16
});

// 2) Start daemons from your ritual’s first block (see above)

// 3) Use IScrivener<MyEntry> as usual
IScrivener<MyEntry> journal = provider.GetRequiredService<IScrivener<MyEntry>>();
await journal.WriteAsync(new MyEntry(/* ... */));
await foreach ((long pos, MyEntry e) in journal.TailAsync())
{
    // application logic ...
}
```

## Notes & Limitations

- Append‑only persistence; no compaction, rotation, or on‑startup replay.
- Single consumer performs file writes; designed for one process to own the file.
- Shutdown flushes any buffered entries. Unhandled exceptions in producer/consumer surface via daemon failure.
