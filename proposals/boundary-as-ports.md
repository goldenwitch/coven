# Boundary as Ports

> **Status**: Draft  
> **Created**: 2026-01-31  
> **Depends on**: inner-covenants.md

---

## Summary

Refactor inner covenant boundaries from **synthetic manifests** to **typed ports**. The boundary becomes a set of explicitly declared edges rather than a fake branch that requires special-case filtering.

---

## Problem

The current `InnerCovenantBuilder` creates a synthetic "Boundary" manifest:

```csharp
public IInnerCovenantBuilder ConnectBoundary()
{
    BranchManifest boundaryManifest = new(
        "Boundary",  // Magic string
        _boundaryJournalType,
        _boundaryProduces,
        _boundaryConsumes,
        []);
    _innerManifests.Add(boundaryManifest);
    return this;
}
```

This causes problems:

1. **Magic string matching** — Code must check `if (manifest.Name == "Boundary")` in multiple places
2. **Inverted semantics** — Boundary's `Produces` are route targets, `Consumes` are route sources (opposite of real branches)
3. **Filtering required** — `CompositeDaemon` must skip boundary when creating scriveners
4. **Not type-safe** — Easy to typo the name or forget to filter

---

## Insight

**Ports are typed edges, not manifests.** The boundary represents the interface between the subgraph and its parent — the set of types that cross the edge:

```
┌─────────────────────────────────────────────────────────┐
│  Outer Covenant                                         │
│                                                         │
│   ┌─────────┐         ┌─────────────────────────────┐  │
│   │  Agents │         │  Spellcasting (Composite)   │  │
│   │         │         │                             │  │
│   │    [out]●────────▶●[in]   ┌──────┐             │  │
│   │         │         │       │ File │             │  │
│   │         │         │       └──┬───┘             │  │
│   │    [in]●◀────────●[out]     │                  │  │
│   │         │         │         ▼                  │  │
│   └─────────┘         │    ┌────────┐              │  │
│                       │    │ Router │              │  │
│                       │    └────────┘              │  │
│                       │                             │  │
│                       └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

- **IN ports**: Types the composite accepts from the outer covenant
- **OUT ports**: Types the composite emits to the outer covenant

---

## Proposed Design

### Remove Synthetic Boundary Manifest

Keep boundary info as separate fields, never add to `_innerManifests`:

```csharp
internal sealed class InnerCovenantBuilder : IInnerCovenantBuilder
{
    // Boundary contract (known at construction)
    private readonly Type _boundaryJournalType;
    private readonly IReadOnlySet<Type> _boundaryProduces;  // OUT ports
    private readonly IReadOnlySet<Type> _boundaryConsumes;  // IN ports
    
    // Real branches only — no synthetic manifests
    private readonly List<BranchManifest> _innerManifests = [];
    
    // Remove: ConnectBoundary()
    // Remove: BoundaryManifestName const
}
```

### Build Entry-to-Journal Mapping Explicitly

```csharp
public void Routes(Action<ICovenant> configure)
{
    // ... collect routes ...
    
    // Build mapping from inner manifests
    Dictionary<Type, Type> entryToJournal = [];
    foreach (BranchManifest manifest in _innerManifests)
    {
        foreach (Type entry in manifest.Produces.Concat(manifest.Consumes))
        {
            entryToJournal[entry] = manifest.JournalEntryType;
        }
    }
    
    // Add boundary types explicitly — no synthetic manifest
    foreach (Type entry in _boundaryProduces.Concat(_boundaryConsumes))
    {
        entryToJournal[entry] = _boundaryJournalType;
    }
    
    // ... validation and pump building ...
}
```

### Simplify CompositeDaemon

No more filtering:

```csharp
// Before: had to skip "Boundary"
foreach (BranchManifest innerManifest in _manifest.InnerManifests)
{
    if (innerManifest.Name == InnerCovenantBuilder.BoundaryManifestName)
        continue;
    CreateScrivenerFor(innerManifest);
}

// After: just iterate
foreach (BranchManifest innerManifest in _manifest.InnerManifests)
{
    CreateScrivenerFor(innerManifest);
}
```

---

## Conditional Composition

A key benefit: boundaries can be **derived from configuration**.

### Use Case: Spellcasting with Optional Substrates

```csharp
public static CompositeBranchManifest UseSpellcasting(
    this ICovenBuilder coven,
    SpellcastingConfig config)
{
    // Build boundary contract based on what's enabled
    HashSet<Type> produces = [typeof(SpellResult)];
    HashSet<Type> consumes = [];
    List<BranchManifest> innerBranches = [];
    
    if (config.FileEnabled)
    {
        consumes.Add(typeof(FileToolRequest));
        produces.Add(typeof(FileToolResult));
        innerBranches.Add(UseFileBranch());
    }
    
    if (config.ComputeEnabled)
    {
        consumes.Add(typeof(ComputeRequest));
        produces.Add(typeof(ComputeResult));
        innerBranches.Add(UseComputeBranch());
    }
    
    return coven.CompositeManifest<SpellcastingEntry, SpellcastingDaemon>(
        "Spellcasting",
        produces,
        consumes,
        inner => 
        {
            foreach (var branch in innerBranches)
                inner.Connect(branch);
            
            inner.Routes(c => 
            {
                if (config.FileEnabled)
                {
                    c.Route<FileToolRequest, InternalFileOp>(...);
                    c.Route<InternalFileResult, FileToolResult>(...);
                }
                if (config.ComputeEnabled)
                {
                    c.Route<ComputeRequest, InternalCompute>(...);
                    c.Route<InternalComputeResult, ComputeResult>(...);
                }
            });
        }
    );
}
```

### Outer Covenant Only Sees Enabled Types

```csharp
var spellcasting = coven.UseSpellcasting(new SpellcastingConfig 
{ 
    FileEnabled = true,
    ComputeEnabled = false
});

coven.Covenant()
    .Connect(agents)
    .Connect(spellcasting)  // Only exposes file types
    .Routes(c => 
    {
        c.Route<AgentToolCall, FileToolRequest>();     // ✓ Valid
        c.Route<FileToolResult, AgentToolResponse>();  // ✓ Valid
        // c.Route<..., ComputeRequest>();             // ✗ Would fail validation
    });
```

---

## Validation Rules

Graph-theoretic validation:

1. **Every IN port must be consumed** — Each `_boundaryConsumes` type must be a route source (or terminal)
2. **Every OUT port must be produced** — Each `_boundaryProduces` type must be a route target
3. **No dangling inner edges** — Inner branch produces/consumes must connect to something
4. **Type uniqueness** — Each entry type belongs to exactly one journal

Error messages remain actionable:

```
FileToolRequest is declared as an IN port but has no route.
  Add: c.Route<FileToolRequest, ...>() or c.Terminal<FileToolRequest>()
```

---

## Migration

1. Remove `ConnectBoundary()` from `IInnerCovenantBuilder`
2. Remove `BoundaryManifestName` const
3. Update `InnerCovenantBuilder.Routes()` to build entry mapping explicitly
4. Update `CompositeBranchManifest.InnerManifests` to exclude boundary
5. Simplify `CompositeDaemon.BuildInnerServiceProvider()`
6. Update tests that called `ConnectBoundary()`

---

## Future: Inferred Ports

Once stable, we could infer ports from routes:

```csharp
inner.Routes(c => 
{
    // Route SOURCE is a boundary type → declares IN port
    c.Route<FileToolRequest, InternalFileOp>(...);
    
    // Route TARGET is a boundary type → declares OUT port
    c.Route<InternalFileResult, FileToolResult>(...);
});
// No explicit port declaration needed — inferred from route endpoints
```

This requires distinguishing "boundary types" from "inner types" — possibly via a marker interface or by checking if the type exists in any connected inner manifest.

---

## References

- [inner-covenants.md](inner-covenants.md) — Parent proposal
- [Covenants-and-Routing.md](../architecture/Covenants-and-Routing.md) — Architecture overview
