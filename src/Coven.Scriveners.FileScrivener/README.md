# Coven.Scriveners.FileScrivener

File-backed `IScrivener<T>` that composes an internal `InMemoryScrivener<T>` and a snapshot-based flushing daemon.

## Design

- `FileScrivener<T>`: wraps `InMemoryScrivener<T>` and exposes the standard journaling API.
- `FlusherDaemon<T>`: tails the scrivener, appends to an in-memory snapshot, and when a configurable predicate is met (default: 100 messages), swaps snapshots under a lock and enqueues for flushing.
- Secondary loop consumes snapshots from a bounded channel (ring buffer) and appends to a file sink using a serializer.

## DI

```csharp
// Registers FileScrivener<T>, flusher daemon, default JSON serializer, and file sink
services.AddFileScrivener<MyEntry>(new FileScrivenerConfig
{
    FilePath = "./journal.ndjson",
    FlushThreshold = 100,
    FlushQueueCapacity = 8
});
```

Start daemons via a MagikBlock as usual (e.g., inject `IEnumerable<ContractDaemon>` and start/shutdown).

