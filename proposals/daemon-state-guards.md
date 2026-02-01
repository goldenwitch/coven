# Daemon State Guards

**Status:** Implemented

Unify daemon lifecycle state management by having infrastructure daemons extend `ContractDaemon`.

## Problem

`CompositeDaemon` and `CovenantAdherentDaemon` both implement `IDaemon` directly with ad-hoc state handling:

- `Start()` guards against bad states ✓
- `Shutdown()` does not guard ✗

This allows:
- Shutdown before Start (no-op but semantically wrong)
- Double shutdown (redundant cleanup, potential null dereferences)

Both daemons duplicate the same state management pattern.

## Proposal

Have `CompositeDaemon` and `CovenantAdherentDaemon` extend `ContractDaemon`.

We considered a lightweight `DaemonBase` without journaling, but the "overhead" of `IScrivener<DaemonEvent>` is negligible—2 writes per daemon lifetime (Start + Shutdown). The simpler hierarchy wins, and all daemons get `WaitFor()` for free if orchestration ever needs it.

## State Transitions

Update `ContractDaemon.Transition()` to enforce:

| From | To | Result |
|------|-----|--------|
| Stopped → Running | ✓ valid | proceed |
| Running → Completed | ✓ valid | proceed |
| Running → Running | ✓ valid | no-op, log |
| Completed → Completed | ✓ valid | no-op, log |
| Stopped → Completed | ✗ invalid | throw |
| Completed → Running | ✗ invalid | throw (already enforced) |

## Scope

- `ContractDaemon` — add idempotent transition handling + shutdown guards
- `CompositeDaemon` — extend `ContractDaemon`
- `CovenantAdherentDaemon` — extend `ContractDaemon`

## Implementation Notes

`CompositeDaemon.Start()` uses a manual guard-then-transition pattern instead of transition-first because inner daemon startup can fail. If we transitioned to Running first and then inner daemons failed, we'd be stuck in Running state since `Running → Stopped` isn't valid. By guarding at the start and only transitioning after successful inner daemon startup, we maintain atomicity: the daemon either starts completely (including inner daemons) and transitions to Running, or stays Stopped.
