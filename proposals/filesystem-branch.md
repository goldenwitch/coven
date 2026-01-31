# FileSystem Sub-branch

> **Status**: Draft  
> **Created**: 2026-01-25  
> **Parent**: [Spellcasting Branch](spellcasting-branch.md)

---

## Summary

Sub-branch of Spellcasting for file operations. Spells write efferent intent (`FileRead`, `FileWrite`). Leaf daemons tail via `TailAsync`, satisfy against their backend, write afferent fulfillment.

---

## Entries

Base: `FileSystemEntry : Entry`

### Efferent (Intent)

| Entry | Purpose |
|-------|---------|
| `FileRead` | Read content (path, offset?, length?) |
| `FileWrite` | Write content (path, content, createMode) |
| `FileList` | List directory (path, pattern?, recursive?) |
| `FileDelete` | Delete (path, recursive?) |
| `FileStat` | Get metadata (path) |

### Afferent (Fulfillment)

| Entry | Purpose |
|-------|---------|
| `FileContent` | Content response |
| `FileWritten` | Write confirmation |
| `FileListing` | Directory entries |
| `FileDeleted` | Delete confirmation |
| `FileMetadata` | Size, modified, created, isDirectory, permissions |
| `FileFault` | Failure (faultKind, path, message) |

All carry `CorrelationId` for matching.

---

## Leaves

Each leaf extends `ContractDaemon`, tails `IScrivener<FileSystemEntry>`, processes intent entries, writes fulfillment:

```
DAEMON LocalFSDaemon
  tails: IScrivener<FileSystemEntry>
  
  ON FileRead { correlation-id, path }:
    content = read file at path
    WRITE FileContent { correlation-id, content }
    
  ON FileWrite { correlation-id, path, content }:
    write content to path
    WRITE FileWritten { correlation-id }
    
  ON error:
    WRITE FileFault { correlation-id, error }
```

| Leaf | Backend |
|------|--------|
| `LocalFSDaemon` | Local disk via `System.IO` |
| `MockFSDaemon` | In-memory (testing) |

Leaves filter by path scope. A leaf rooted at `/workspace` ignores paths outside that prefix.

---

## Checklist

- [ ] `FileSystemEntry` hierarchy with `[JsonPolymorphic]`
- [ ] `LocalFSDaemon` extends `ContractDaemon`
- [ ] `MockFSDaemon` for testing
- [ ] Path scoping configuration
