# Daemonology Consolidation

**Status: Implemented**

Consolidate `Coven.Daemonology` into `Coven.Core` under the namespace `Coven.Core.Daemonology`.

## Motivation

Daemons are now central to Coven's architecture. The covenant system, composite branches, and streaming all rely on daemon lifecycle management. The current package separation creates friction:

1. **Circular dependency prevents code reuse.** `CompositeDaemon` and `CovenantAdherentDaemon` in Core cannot extend `ContractDaemon` because Daemonology depends on Core. Both currently implement ad-hoc state guards that should be shared.

2. **Wide adoption signals core status.** Eleven packages reference Daemonology. When nearly everything depends on something, that something belongs in Core.

3. **The separation no longer reflects reality.** The original split implied daemons were optional infrastructure. They're not—they're how Coven runs anything long-lived.

## What Changes

- `ContractDaemon`, `DaemonEvent`, and `Status` move into `Coven.Core.Daemonology`
- `IDaemon` stays where it is (already in `Coven.Core`)
- Consuming packages drop their Daemonology reference (they already have Core)
- Tests merge into `Coven.Core.Tests`

## Follow-on Work

Once consolidated, we can address the original PR #62 feedback:

- Have `CompositeDaemon` and `CovenantAdherentDaemon` extend `ContractDaemon` (or a lighter `DaemonBase` if the scrivener requirement is too heavy for infra daemons)
- Add proper shutdown guards to the base class rather than duplicating them
- Clean up ServiceProvider leak and pump task handling in `CompositeDaemon`

## Non-Goals

- Changing daemon semantics or the `IDaemon` contract
- Renaming anything beyond the namespace change
- Refactoring how daemons are registered or discovered
