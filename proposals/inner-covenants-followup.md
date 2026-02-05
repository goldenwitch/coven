# Inner Covenants: Follow-up Items

> **Status**: Superseded by unified-covenant-builder.md

**Note**: This proposal is obsolete. The entire inner covenant infrastructure was deleted in favor of a flat covenant model. The items below are no longer applicable:

- ~~State guard in CompositeDaemon.Shutdown()~~ → CompositeDaemon deleted
- ~~Duplicate key handling~~ → Fixed: entry-journal uniqueness validation now in CovenantBuilder
- ~~ServiceProvider leak~~ → CompositeDaemon deleted

---

*Original content preserved for historical context:*

Tracked from PR #62 review feedback. These are non-blocking improvements to address in subsequent work.

## Should-Fix

### 1. State guard in CompositeDaemon.Shutdown()
**File:** `src/Coven.Core/Covenants/CompositeDaemon.cs`

Currently `Shutdown()` doesn't guard against being called before `Start()` or being called multiple times. Should mirror the state management pattern used elsewhere.

### 2. Duplicate key handling in entryToJournal dictionary
**File:** `src/Coven.Core/Covenants/CovenantBuilder.cs`

In `Routes()`, the `entryToJournal` dictionary is built with `.ToDictionary()` which throws on duplicate keys. If two branches produce/consume the same entry type, this fails silently with an unclear exception. Should either:
- Detect and report as a validation error, or
- Use first/last-wins with clear documentation

### 3. ServiceProvider leak on Start failure
**File:** `src/Coven.Core/Covenants/CompositeDaemon.cs`

If `Start()` fails partway through (e.g., after creating the `ServiceProvider` but before completing daemon startup), the `ServiceProvider` may not be disposed. Consider try/catch with cleanup.

### 4. Pump task handling on failure path
**File:** `src/Coven.Core/Covenants/CompositeDaemon.cs`

If starting pumps fails, previously started pump tasks may be orphaned. Should ensure all started tasks are properly awaited/cancelled on failure.

## Questions from Review

- Should `CompositeDaemon` expose inner journal access for testing/debugging?
- Is the boundary manifest validation strict enough (entry types vs full manifest comparison)?
- Should `InnerCovenantBuilder` support conditional branches (feature flags)?

## Status

- [ ] Item 1: State guard
- [ ] Item 2: Duplicate keys
- [ ] Item 3: ServiceProvider leak
- [ ] Item 4: Pump task cleanup
