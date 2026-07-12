# Coven.FileSystem

FileSystem branch for Coven — entry types that model file I/O as journal entries flowing through the covenant graph.

## What's Inside

- `FileSystemEntry`: abstract base record (polymorphic, JSON‑serialisable).
- `FileRead`: efferent command requesting a file's contents by path.
- `FileContent`: afferent result carrying the file's text.
- `FileFailure`: afferent result signalling an error (kind + message).

All entries carry a `CorrelationId` so callers can match requests to responses.

## Why use it?

- **Provider‑agnostic**: define file operations once; swap the leaf (POSIX today, cloud tomorrow) without touching consuming code.
- **Journal‑native**: file I/O becomes append‑only entries, inheriting Coven's tailing, windowing, and snapshot capabilities.
- **Testable**: write `FileRead` entries and assert on `FileContent` / `FileFailure` without touching the real file system.

## Usage

```csharp
// Write a read command into the FileSystem journal
await journal.WriteAsync(new FileRead("corr-1", "src/Program.cs"), ct);

// Tail for the result
await foreach ((long _, FileSystemEntry? entry) in journal.TailAsync(0, ct))
{
    switch (entry)
    {
        case FileContent c:
            Console.WriteLine(c.Content);
            break;
        case FileFailure f:
            Console.Error.WriteLine($"{f.FailureKind}: {f.Message}");
            break;
    }
}
```

## See Also

- Leaf implementation: `Coven.FileSystem.Posix` (POSIX / System.IO).
- Agent bridge: `Coven.Agents.FileSystem` (tool‑call ↔ journal routing).
- Architecture: Journaling and Scriveners; Abstractions and Branches.
