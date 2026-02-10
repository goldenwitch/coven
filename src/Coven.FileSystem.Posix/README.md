# Coven.FileSystem.Posix

POSIX leaf for the Coven FileSystem branch — sandboxed file I/O backed by `System.IO`.

## What's Inside

- `PosixFileSystemDaemon`: leaf daemon that tails the FileSystem journal, services `FileRead` commands, and writes `FileContent` / `FileFailure` entries back.
- `PosixFileOperations`: sandboxed read logic with path resolution confined to a configured root directory.
- `FileOperationResult`: internal discriminated union (`Success`, `NotFound`, `ReadFailed`).
- `PosixFileSystemConfig`: configuration POCO (`Root` — the sandbox boundary).
- `UsePosixFileSystem`: extension method on `CovenServiceBuilder` that registers the journal, config, operations, and daemon and returns a `BranchManifest`.

## Why use it?

- **Sandboxed**: all paths are resolved and confined to the configured root; traversal outside the boundary is rejected.
- **Turnkey setup**: a single `UsePosixFileSystem(root)` call wires everything up and returns a manifest the covenant can consume.
- **Observable**: source‑generated logging (`PosixFileSystemLog`) emits structured events for reads and failures.

## Usage

```csharp
using Coven.Core.Builder;
using Coven.FileSystem.Posix;

services.BuildCoven(c =>
{
    BranchManifest fs = c.UsePosixFileSystem("/repo");
    // fs.Produces: FileContent, FileFailure
    // fs.Consumes: FileRead

    c.Done();
});
```

## Testing

- Supply a temporary directory as the root and seed it with known files.
- Write `FileRead` entries into the journal and assert on the resulting `FileContent` / `FileFailure` entries.
- Verify sandbox confinement by requesting paths with `..` segments and expecting `FileFailure`.

## See Also

- Branch types: `Coven.FileSystem`.
- Agent bridge: `Coven.Agents.FileSystem`.
- Architecture: Journaling and Scriveners; Leaves and Daemons.
