# Move IDaemon Interface to Coven.Core

> **Status**: Implemented  
> **Created**: 2026-01-24  
> **Implemented**: 2026-01-24  
> **Blocking**: [daemon-scope-auto-start.md](daemon-scope-auto-start.md), [declarative-covenants.md](declarative-covenants.md)

---

## Problem

The `IDaemon` interface currently lives in `Coven.Daemonology`, but daemon lifecycle management needs to happen in `Coven.Core` (specifically in `CovenExecutionScope`). This creates a circular dependency:

```
Current dependency graph:

    Coven.Core  ◄──  Coven.Daemonology
    (root)           (depends on Core)

Required by daemon-scope-auto-start:

    Coven.Core  ◄──► Coven.Daemonology
                ↑
          CIRCULAR - won't compile
```

The [daemon-scope-auto-start](daemon-scope-auto-start.md) proposal requires `CovenExecutionScope` (in Core) to hold and manage `IDaemon` instances. Without moving the interface, this is impossible.

---

## Solution

Move the `IDaemon` interface from `Coven.Daemonology` to `Coven.Core`.

### What Moves

| Type | From | To |
|------|------|-----|
| `IDaemon` | Coven.Daemonology | Coven.Core |
| `Status` enum | Coven.Daemonology | Coven.Core |

### What Stays

| Type | Location | Reason |
|------|----------|--------|
| `ContractDaemon` | Coven.Daemonology | Abstract base with implementation details |
| `DaemonEvent` | Coven.Daemonology | Specific to daemon journaling |
| Concrete daemons | Various projects | Implementation details |

---

## Interface Definition

```csharp
// Coven.Core/IDaemon.cs
namespace Coven;

/// <summary>
/// A long-running service that can be started and stopped.
/// Daemons are scoped to a ritual's execution scope.
/// </summary>
public interface IDaemon
{
    /// <summary>
    /// Current lifecycle status of the daemon.
    /// </summary>
    Status Status { get; }
    
    /// <summary>
    /// Start the daemon. The daemon should be ready to perform work
    /// when this method returns.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token that signals the daemon should stop. Daemons should use
    /// CancellationTokenSource.CreateLinkedTokenSource to create internal
    /// tokens that respect both this token and their own shutdown logic.
    /// </param>
    Task Start(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gracefully stop the daemon. Called after the scope's CancellationToken
    /// has been cancelled, so internal loops should already be winding down.
    /// </summary>
    Task Shutdown(CancellationToken cancellationToken = default);
}

/// <summary>
/// Lifecycle status of a daemon.
/// </summary>
public enum Status
{
    /// <summary>Daemon has not been started or has been stopped.</summary>
    Stopped,
    
    /// <summary>Daemon is running and processing work.</summary>
    Running,
    
    /// <summary>Daemon has completed its work normally.</summary>
    Completed
}
```

---

## Migration

### Phase 1: Add to Core

1. Add `IDaemon.cs` and `Status.cs` to `Coven.Core`
2. Keep copies in `Coven.Daemonology` temporarily (to avoid breaking existing code)

### Phase 2: Update References

1. Update all `using` statements to reference `Coven` namespace
2. `ContractDaemon` continues to implement `IDaemon` (now from Core)
3. Verify all projects compile

### Phase 3: Remove from Daemonology

1. Delete `IDaemon.cs` from `Coven.Daemonology`
2. Delete `Status.cs` from `Coven.Daemonology`
3. Final verification

---

## Impact

### Dependency Graph After

```
    Coven.Core          (contains IDaemon, Status)
         ▲
         │
    Coven.Daemonology   (contains ContractDaemon, implements IDaemon)
         ▲
         │
    Coven.Agents.OpenAI (contains OpenAIDaemon : ContractDaemon)
```

No circular dependencies. `CovenExecutionScope` can now hold `IReadOnlyList<IDaemon>`.

### Breaking Changes

**None for consumers.** The interface and enum are identical; only the assembly changes.

Projects that explicitly reference `Coven.Daemonology.IDaemon` will need to update to `Coven.IDaemon`, but:
- The namespace is just `Coven` in both cases
- Most code uses `using Coven;` and won't notice

---

## Rationale

`IDaemon` belongs in Core because:

1. **Ubiquitous usage** — Daemons are fundamental to the coven pattern
2. **Lifecycle management** — `CovenExecutionScope` needs to manage daemon lifecycle
3. **No implementation details** — The interface is pure contract, no dependencies
4. **Enables composition** — Other Core types can reference `IDaemon` without pulling in Daemonology

`ContractDaemon` stays in Daemonology because:

1. **Implementation concerns** — Status transitions, journaling, failure tracking
2. **Dependencies** — Needs `IScrivener<DaemonEvent>` and other Daemonology types
3. **Optional** — Not all daemons need the contract base class

---

## Related Proposals

- **[daemon-scope-auto-start.md](daemon-scope-auto-start.md)** — Blocked by this; needs `IDaemon` in Core for `DaemonScope`
- **[declarative-covenants.md](declarative-covenants.md)** — Indirectly blocked; depends on daemon-scope-auto-start
