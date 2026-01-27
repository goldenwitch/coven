# FileSystem Branch

> **Status**: Draft  
> **Created**: 2026-01-25  
> **Depends on**: Spellcasting and Target Branches (tooling-branch.md)

---

## Summary

Define the **FileSystem Branch** — one of the target branches in Coven's two-hop spellcasting architecture. This branch abstracts file operations through typed journal entries, with platform-specific leaves for local disk (`PosixFS`, `WindowsFS`).

---

## Problem

Agents need to read, write, and manage files. But file systems vary wildly:

| Substrate | Path Separators | Root Concept | Binary Handling | Error Model |
|-----------|----------------|--------------|-----------------|-------------|
| Windows NTFS | `\` | Drive letters (`C:\`) | Implicit | `Win32Exception` |
| Unix/Linux | `/` | Single root (`/`) | Implicit | `errno` codes |
| SFTP | `/` | Connection root | Binary streams | SSH errors |
| S3 | `/` | Bucket prefix | Byte arrays | HTTP status |
| In-memory | `/` | Virtual root | Byte arrays | Exceptions |

Without abstraction, spell implementations would couple to specific backends, making testing difficult and deployment inflexible.

### Goals

1. **Backend-agnostic file operations** — Spells write efferent entries; leaves handle the substrate
2. **Auditable via journals** — Every file operation is recorded and replayable
3. **Clear error taxonomy** — Faults are typed and actionable, not opaque exceptions

---

## Design

### Branch Position in Architecture

```
                    Spellcasting Branch
                           │
                           │ SpellInvocation / SpellResult
                           ▼
           ┌───────────────────────────────┐
           │        Spell Covenants        │
           │   (read_file, write_file...)  │
           └───────────────────────────────┘
                           │
                           ▼
           ┌───────────────────────────────┐
           │      FileSystem Branch        │  ← THIS PROPOSAL
           │   (abstract file operations)  │
           └───────────────────────────────┘
                           │
              ┌────────────┴────────────┐
              │                         │
              ▼                         ▼
       ┌─────────────┐          ┌─────────────┐
       │  LocalFS    │          │   MockFS    │
       │   Leaf      │          │    Leaf     │
       └─────────────┘          └─────────────┘
              │                         │
              ▼                         ▼
         Local Disk             In-Memory Store
```

### Entry Types

All entries carry a `CorrelationId` for request/response matching.

#### Efferent (Spine → Leaf)

| Entry | Fields | Purpose |
|-------|--------|---------|
| `FileRead` | `CorrelationId`, `Path`, `Offset?`, `Length?` | Request file content (optional range) |
| `FileWrite` | `CorrelationId`, `Path`, `Content`, `CreateMode` | Write content to path |
| `FileList` | `CorrelationId`, `Path`, `Pattern?`, `Recursive?` | List directory contents |
| `FileDelete` | `CorrelationId`, `Path`, `Recursive?` | Delete file or directory |
| `FileStat` | `CorrelationId`, `Path` | Request file/directory metadata |

#### Afferent (Leaf → Spine)

| Entry | Fields | Purpose |
|-------|--------|---------|
| `FileContent` | `CorrelationId`, `Content`, `TotalSize`, `IsPartial` | File content response |
| `FileWritten` | `CorrelationId`, `Path`, `BytesWritten` | Write confirmation |
| `FileListing` | `CorrelationId`, `Entries[]` | Directory listing response |
| `FileDeleted` | `CorrelationId`, `Path`, `DeletedCount` | Delete confirmation |
| `FileMetadata` | `CorrelationId`, `Size`, `Modified`, `Created`, `IsDirectory`, `Permissions` | Metadata response |
| `FileFault` | `CorrelationId`, `FaultKind`, `Path`, `Message` | Operation failure |

### Correlation IDs

Every efferent entry includes a `CorrelationId` (opaque string). The corresponding afferent entry echoes it back:

```
┌──────────────────┐         ┌──────────────────┐
│  FileRead        │         │  FileContent     │
│  CorrelationId:  │────────►│  CorrelationId:  │
│    "req-42"      │         │    "req-42"      │
│  Path: "/a.txt"  │         │  Content: "..."  │
└──────────────────┘         └──────────────────┘
```

The spell or daemon that wrote the efferent entry is responsible for generating a unique correlation ID and matching it when tailing afferent entries.

**Generation strategy:** Use `Guid.NewGuid().ToString("N")` or a similar scheme. The branch imposes no format — correlation IDs are opaque to leaves.

### Path Semantics

**All paths are branch-relative and use forward slashes.**

| Path | Meaning |
|------|---------|
| `file.txt` | File in current context |
| `src/main.cs` | Nested path |
| `/config.json` | Absolute from branch root |
| `../other/file` | Invalid — no parent traversal |

**Leaves translate to substrate paths:**

```
Branch Path:   /src/main.cs
    │
    ▼ (LocalFS with root = "C:\Projects\MyApp")
    │
Substrate:     C:\Projects\MyApp\src\main.cs
```

**Constraints:**
- No `..` components (prevents root escape)
- No empty components (`foo//bar` is invalid)
- Case sensitivity is leaf-dependent (PosixFS is case-sensitive; WindowsFS is not)
- Max path length is leaf-dependent

Invalid paths result in `FileFault` with `FaultKind.InvalidPath`.

### Content Encoding

**Binary-first design.** Content is always a byte sequence.

| Entry | Content Field | Encoding |
|-------|--------------|----------|
| `FileContent` | `byte[]` or stream reference | Raw bytes |
| `FileWrite` | `byte[]` or stream reference | Raw bytes |

**Text handling is caller responsibility.** Spells that work with text encode/decode using their chosen encoding. The branch doesn't assume encoding.

For large files, see Streaming section below.

### CreateMode (Write Behavior)

`FileWrite.CreateMode` controls behavior when the target exists:

| Mode | Target Exists | Target Missing |
|------|--------------|----------------|
| `CreateNew` | Fault | Create |
| `CreateOrReplace` | Overwrite | Create |
| `CreateOrAppend` | Append | Create |
| `ReplaceExisting` | Overwrite | Fault |
| `AppendExisting` | Append | Fault |

### Error Taxonomy

`FileFault.FaultKind` classifies failures:

| FaultKind | Meaning | Typical Cause |
|-----------|---------|---------------|
| `NotFound` | Path doesn't exist | Read/delete nonexistent file |
| `AlreadyExists` | Path exists unexpectedly | `CreateNew` on existing file |
| `AccessDenied` | Permission denied | OS permissions, locked file |
| `InvalidPath` | Path violates constraints | `..` traversal, invalid chars |
| `DirectoryNotEmpty` | Non-recursive delete of non-empty dir | `Recursive=false` on dir with contents |
| `NotADirectory` | Expected directory, found file | `FileList` on a file path |
| `NotAFile` | Expected file, found directory | `FileRead` on a directory |
| `DiskFull` | No space remaining | Write to full disk |
| `IoError` | Unclassified I/O failure | Hardware error, network timeout |
| `PathTooLong` | Path exceeds substrate limits | Very deep nesting |
| `Timeout` | Operation timed out | Slow network filesystem |

Faults are deterministic — the same operation on the same state produces the same fault kind.

### Streaming for Large Files

For files exceeding a threshold (leaf-configurable, default 1MB), content streams via chunks:

```
FileRead(Path: "/large.bin", CorrelationId: "req-1")
    │
    ▼
FileContentChunk(CorrelationId: "req-1", Offset: 0, Data: [...], IsFinal: false)
FileContentChunk(CorrelationId: "req-1", Offset: 65536, Data: [...], IsFinal: false)
FileContentChunk(CorrelationId: "req-1", Offset: 131072, Data: [...], IsFinal: true)
```

**Additional entry types for streaming:**

| Entry | Direction | Fields |
|-------|-----------|--------|
| `FileContentChunk` | Afferent | `CorrelationId`, `Offset`, `Data`, `IsFinal` |
| `FileWriteChunk` | Efferent | `CorrelationId`, `Offset`, `Data`, `IsFinal` |

**Chunked writes:** Caller sends multiple `FileWriteChunk` entries with same correlation ID. Leaf buffers and writes on `IsFinal=true`, then emits `FileWritten`.

**Small files:** Still emit a single `FileContent`/`FileWritten` — no chunking overhead.

---

## Platform-Specific Leaves

POSIX and Windows file systems differ fundamentally:

| Concern | POSIX | Windows |
|---------|-------|--------|
| Path separator | `/` | `\` (but `/` often works) |
| Root concept | Single `/` | Drive letters (`C:\`, `D:\`) |
| Case sensitivity | Yes (usually) | No (NTFS default) |
| Permission model | `rwx` for user/group/other | ACLs |
| Symlinks | Native | Requires elevated privileges |
| Reserved names | None | `CON`, `PRN`, `NUL`, etc. |
| Max path | 4096 typical | 260 (classic) or 32767 (long path) |
| Line endings | `LF` | `CRLF` (by convention) |
| File locking | Advisory | Mandatory |

A single "LocalFS" would hide these differences poorly. Platform-specific leaves handle them explicitly.

---

## Leaf: PosixFS

File I/O for POSIX-compliant systems (Linux, macOS, BSD).

### Configuration

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `RootPath` | `string` | Required | Absolute path to sandbox root |
| `AllowWrite` | `bool` | `true` | Enable write operations |
| `AllowDelete` | `bool` | `true` | Enable delete operations |
| `ChunkSize` | `int` | `65536` | Bytes per streaming chunk |
| `StreamingThreshold` | `long` | `1048576` | Files larger than this stream |
| `FollowSymlinks` | `bool` | `true` | Resolve symlinks or treat as opaque |
| `PreservePermissions` | `bool` | `false` | Maintain `rwx` bits on copy |

### Daemon: `PosixFSDaemon`

```
┌─────────────────────────────────────────────────────────┐
│                    PosixFSDaemon                        │
│                                                         │
│  Lifecycle: Stopped → Running → Completed               │
│                                                         │
│  Tails:   IScrivener<FileSystemEntry> (efferent)        │
│  Writes:  IScrivener<FileSystemEntry> (afferent)        │
│                                                         │
│  Platform-specific handling:                            │
│    - Paths are case-sensitive                           │
│    - Symlinks resolved or reported based on config      │
│    - Permission bits (mode) included in FileMetadata    │
│    - Atomic writes via rename(2) on same filesystem     │
│    - Advisory locking with flock(2) if needed           │
└─────────────────────────────────────────────────────────┘
```

### Path Translation (POSIX)

```
Input:     /src/main.c
RootPath:  /home/user/project

Step 1: Validate (no ..)        → OK
Step 2: Join with root          → /home/user/project/src/main.c
Step 3: Realpath to resolve symlinks
Step 4: Verify still under root → OK (prevents symlink escape)
```

### Symlink Handling

| `FollowSymlinks` | `FileRead` behavior | `FileStat` behavior |
|------------------|---------------------|---------------------|
| `true` | Read target content | Report target metadata |
| `false` | Read target content | Report link metadata (`IsSymlink: true`) |

Symlinks pointing outside `RootPath` → `FileFault(AccessDenied)`.

---

## Leaf: WindowsFS

File I/O for Windows systems (NTFS, ReFS).

### Configuration

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `RootPath` | `string` | Required | Absolute path (e.g., `C:\Projects\Sandbox`) |
| `AllowWrite` | `bool` | `true` | Enable write operations |
| `AllowDelete` | `bool` | `true` | Enable delete operations |
| `ChunkSize` | `int` | `65536` | Bytes per streaming chunk |
| `StreamingThreshold` | `long` | `1048576` | Files larger than this stream |
| `UseLongPaths` | `bool` | `true` | Enable paths beyond 260 chars |

### Daemon: `WindowsFSDaemon`

```
┌─────────────────────────────────────────────────────────┐
│                   WindowsFSDaemon                       │
│                                                         │
│  Lifecycle: Stopped → Running → Completed               │
│                                                         │
│  Tails:   IScrivener<FileSystemEntry> (efferent)        │
│  Writes:  IScrivener<FileSystemEntry> (afferent)        │
│                                                         │
│  Platform-specific handling:                            │
│    - Paths are case-insensitive (NTFS default)          │
│    - Reserved names rejected (CON, PRN, NUL, etc.)      │
│    - Long path prefix (\\?\) added when UseLongPaths    │
│    - ACLs not exposed (simplified permission model)     │
│    - Mandatory file locking handled with retries        │
│    - Atomic writes via MoveFileEx with REPLACE_EXISTING │
└─────────────────────────────────────────────────────────┘
```

### Path Translation (Windows)

```
Input:     /src/Program.cs
RootPath:  C:\Projects\MyApp

Step 1: Convert separators      → \src\Program.cs
Step 2: Validate (no .., no reserved names)
Step 3: Join with root          → C:\Projects\MyApp\src\Program.cs
Step 4: Add long path prefix    → \\?\C:\Projects\MyApp\src\Program.cs
Step 5: Verify still under root → OK
```

### Reserved Name Handling

Windows forbids certain names: `CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`.

`FileWrite` or `FileRead` targeting reserved names → `FileFault(InvalidPath)`.

### File Locking

Windows uses mandatory locking. If a file is locked by another process:

| Operation | Behavior |
|-----------|----------|
| `FileRead` | Retry with backoff, then `FileFault(AccessDenied)` |
| `FileWrite` | Retry with backoff, then `FileFault(AccessDenied)` |
| `FileDelete` | `FileFault(AccessDenied)` immediately |

---

## Journal Design

Single journal for all FileSystem entries:

```
IScrivener<FileSystemEntry>

where FileSystemEntry =
    | FileRead
    | FileContent
    | FileContentChunk
    | FileWrite
    | FileWriteChunk
    | FileWritten
    | FileList
    | FileListing
    | FileDelete
    | FileDeleted
    | FileStat
    | FileMetadata
    | FileFault
```

**Directionality filtering:** Consumers filter by entry type. Spells write efferent, tail afferent. Leaves tail efferent, write afferent.

---

## Alternatives Considered

### 1. Separate Journals per Operation Type

**Considered:** One journal for reads, one for writes, etc.

**Rejected:** Adds complexity without benefit. Correlation IDs already allow precise matching. Single journal simplifies replay and auditing.

### 2. Text-First Content Model

**Considered:** Default to UTF-8 strings, separate binary mode.

**Rejected:** Creates encoding bugs. Binary-first is honest about what files contain. Callers that know their encoding can decode.

### 3. Sync vs Async Entries

**Considered:** Synchronous request/response instead of journal-based.

**Rejected:** Violates Coven's journal-first architecture. Journals enable replay, auditing, and decoupled testing.

### 4. Path Objects Instead of Strings

**Considered:** Strongly-typed path objects with validation.

**Deferred:** Could add later if string paths prove error-prone. For now, validation happens at leaf boundary, keeping entries simple.

---

## Security Considerations

1. **Root escape:** Leaves MUST validate that translated paths remain under their root. Symlinks can escape — consider `realpath` checks.

2. **Resource exhaustion:** Leaves should respect disk quotas.

3. **Permission model:** Leaves run with daemon process permissions. Consider sandboxing options for untrusted spells.

4. **Path injection:** Validate paths before use. Reject null bytes, control characters, reserved names (Windows: `CON`, `PRN`, etc.).

---

## Checklist

### Branch Infrastructure
- [ ] Define `FileSystemEntry` discriminated union
- [ ] Implement `IScrivener<FileSystemEntry>` registration
- [ ] Define correlation ID generation utility

### PosixFS Leaf
- [ ] Implement `PosixFSConfig` with validation
- [ ] Implement `PosixFSDaemon` with lifecycle
- [ ] POSIX path normalization and validation
- [ ] FileRead with streaming support
- [ ] FileWrite with CreateMode handling
- [ ] FileList with glob patterns
- [ ] FileDelete with recursive option
- [ ] FileStat with permission bits
- [ ] Symlink handling (follow vs report)
- [ ] Atomic write via rename(2)
- [ ] Error mapping from errno to FaultKind

### WindowsFS Leaf
- [ ] Implement `WindowsFSConfig` with validation
- [ ] Implement `WindowsFSDaemon` with lifecycle
- [ ] Windows path normalization (separators, long paths)
- [ ] Reserved name rejection (CON, PRN, NUL, etc.)
- [ ] FileRead with streaming support
- [ ] FileWrite with CreateMode handling
- [ ] FileList with glob patterns
- [ ] FileDelete with recursive option
- [ ] FileStat implementation
- [ ] Mandatory file locking retry logic
- [ ] Atomic write via MoveFileEx
- [ ] Error mapping from Win32 to FaultKind

### Testing
- [ ] Unit tests for path validation (both platforms)
- [ ] Unit tests for each entry type round-trip
- [ ] Integration tests: PosixFS against temp directory (Linux/macOS)
- [ ] Integration tests: WindowsFS against temp directory (Windows)
- [ ] E2E tests: Spell → FileSystem → Leaf → result

### Documentation
- [ ] Package README with usage examples
- [ ] Configuration reference
- [ ] Error handling guide
