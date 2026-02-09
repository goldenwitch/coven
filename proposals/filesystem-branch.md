# FileSystem Branch

> **Status**: Revised  
> **Created**: 2026-01-25  
> **Revised**: 2026-02-09

---

## Summary

Branch for file operations. Defines efferent entries (`FileRead`, `FileWrite`) and afferent entries (`FileContent`, `FileFailure`). Leaves translate these to concrete backends.

Branch package: `Coven.Spellcasting.FileSystem`  
Companion: `Coven.Agents.FileSystem` (see [Spellcasting](spellcasting-branch.md))

---

## Entries

Base: `FileSystemEntry : Entry`

### Efferent

| Entry | Purpose |
|-------|---------|
| `FileRead` | Read content (path, offset?, length?) |
| `FileWrite` | Write content (path, content, createMode) |
| `FileList` | List directory (path, pattern?, recursive?) |
| `FileDelete` | Delete (path, recursive?) |
| `FileStat` | Get metadata (path) |

### Afferent

| Entry | Purpose |
|-------|---------|
| `FileContent` | Content response |
| `FileWritten` | Write confirmation |
| `FileListing` | Directory entries |
| `FileDeleted` | Delete confirmation |
| `FileMetadata` | Size, modified, created, isDirectory, permissions |
| `FileFailure` | Failure (failureKind, path, message) |

All carry `CorrelationId` for matching.

---

## Leaves

Each leaf extends `ContractDaemon`, tails `IScrivener<FileSystemEntry>`, processes efferent entries, writes afferent results:

```
DAEMON PosixFSDaemon
  tails: IScrivener<FileSystemEntry>
  
  ON FileRead { correlation-id, path }:
    content = read file at path
    WRITE FileContent { correlation-id, content }
    
  ON FileWrite { correlation-id, path, content }:
    write content to path
    WRITE FileWritten { correlation-id }
    
  ON error:
    WRITE FileFailure { correlation-id, error }
```

| Leaf | Backend | Package |
|------|---------|--------|
| `PosixFSDaemon` | Local disk via `System.IO` (POSIX) | `Coven.Spellcasting.FileSystem.Posix` |
| `WindowsFSDaemon` | Local disk via `System.IO` (Windows) | `Coven.Spellcasting.FileSystem.Windows` |
| `MockFSDaemon` | In-memory (testing) | `Coven.Spellcasting.FileSystem.Mock` |

Leaves filter by path scope. A leaf rooted at `/workspace` ignores paths outside that prefix.

---

## Checklist

- [ ] `FileSystemEntry` hierarchy with `[JsonPolymorphic]`
- [ ] `PosixFSDaemon` extends `ContractDaemon`
- [ ] `MockFSDaemon` for testing
- [ ] Path scoping configuration
- [ ] `Coven.Agents.FileSystem` companion with tool definitions and transmuters
