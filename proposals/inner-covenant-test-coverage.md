# Inner Covenant Test Coverage

> **Status**: Draft  
> **Created**: 2026-01-31  
> **Depends on**: inner-covenants.md, boundary-as-ports.md

---

## Summary

Define the validation guarantees `InnerCovenantBuilder` must provide and ensure each is covered by tests. The builder validates inner covenant structure at DI time, catching configuration errors before runtime.

---

## Problem

Current test coverage validates basic scenarios but misses:
- Multi-branch routing (real composites have multiple inner branches)
- Entry type uniqueness enforcement (recently added, untested)
- Edge cases that could cause silent runtime failures

Without comprehensive tests, refactors risk breaking validation guarantees invisibly.

---

## Validation Rules

The builder enforces five validation rules. Each must have dedicated test coverage.

### Rule 1: Inner Produces Coverage

Every type in an inner manifest's `produces` must have either:
- A route FROM it, OR
- A terminal declaration

**Intent:** Nothing produced goes nowhere. Every output has a destination.

| Scenario | Expected |
|----------|----------|
| Inner produces type with route | ✓ Pass |
| Inner produces type with terminal | ✓ Pass |
| Inner produces type with neither | ✗ Fail with actionable error |

### Rule 2: Route Uniqueness

Each source type may have only one route.

**Intent:** Deterministic flow. No ambiguity about where a type goes.

| Scenario | Expected |
|----------|----------|
| Single route per source | ✓ Pass |
| Two routes from same source | ✗ Fail with "multiple routes" error |

### Rule 3: Inner Consumes Satisfaction

Every type in an inner manifest's `consumes` must be a route target.

**Intent:** Nothing consumed appears from nowhere. Every input has a source.

| Scenario | Expected |
|----------|----------|
| Inner consumes type is route target | ✓ Pass |
| Inner consumes type with no route to it | ✗ Fail with actionable error |

### Rule 4: Boundary OUT Port Fulfillment

Every type in `boundaryProduces` must be either:
- A route target, OR
- A terminal (written directly by inner daemons)

**Intent:** The composite delivers what it promises to the outer covenant.

| Scenario | Expected |
|----------|----------|
| Boundary produces with route to it | ✓ Pass |
| Boundary produces with terminal | ✓ Pass |
| Boundary produces with neither | ✗ Fail with actionable error |

### Rule 5: Boundary IN Port Consumption

Every type in `boundaryConsumes` must be a route source.

**Intent:** The composite handles everything it accepts from the outer covenant.

| Scenario | Expected |
|----------|----------|
| Boundary consumes with route from it | ✓ Pass |
| Boundary consumes with no route | ✗ Fail with actionable error |

---

## Entry Type Uniqueness

Each entry type must belong to exactly one journal. Overlap causes ambiguous pump construction.

| Scenario | Expected |
|----------|----------|
| Entry type in one manifest only | ✓ Pass |
| Entry type in two inner manifests | ✗ Fail with "multiple journals" error |
| Entry type in inner AND boundary | ✗ Fail with "multiple journals" error |

---

## Builder State Machine

The builder enforces a specific call order.

```
┌─────────┐    Branch()     ┌───────────┐    Routes()    ┌──────────┐
│ Initial │───────────────▶│ Building  │───────────────▶│ Sealed   │
└─────────┘    Connect()    └───────────┘                └──────────┘
                  │                                            │
                  │         Branch()                           │
                  └─────────Connect()───────▶ ✗ InvalidOp     │
                                                               │
                            Routes() ───────────────────▶ ✗ InvalidOp
```

| Scenario | Expected |
|----------|----------|
| Branch() before Routes() | ✓ Pass |
| Connect() before Routes() | ✓ Pass |
| Branch() after Routes() | ✗ InvalidOperationException |
| Connect() after Routes() | ✗ InvalidOperationException |
| Routes() called twice | ✗ InvalidOperationException |

---

## Multi-Branch Scenarios

Real composites route between multiple inner branches.

### Linear Chain

```
Boundary ──▶ BranchA ──▶ BranchB ──▶ Boundary
   IN          │           │          OUT
               └───────────┘
```

| Scenario | Expected |
|----------|----------|
| A produces → B consumes → Boundary out | ✓ Pass |
| A produces but B doesn't consume it | ✗ Rule 1 violation |

### Fan-Out

```
              ┌──▶ BranchA ──┐
Boundary ─────┤              ├──▶ Boundary
   IN         └──▶ BranchB ──┘       OUT
```

| Scenario | Expected |
|----------|----------|
| Input routes to both A and B | ✗ Rule 2 violation (duplicate source) |
| Input routes to A only, B gets different input | ✓ Pass |

### Diamond

```
              ┌──▶ BranchA ──┐
Boundary ─────┤              ├──▶ Merge ──▶ Boundary
   IN         └──▶ BranchB ──┘              OUT
```

Requires a merge type that both A and B produce. Validation depends on whether merge is in boundary or inner branch.

---

## Edge Cases

### Empty Boundaries

| Scenario | Expected |
|----------|----------|
| Empty `boundaryProduces` (sink composite) | ✓ Pass if inner types handled |
| Empty `boundaryConsumes` (source composite) | ✓ Pass if outputs produced |
| Both empty (isolated subgraph) | ✓ Pass if internally consistent |

### No Inner Branches

| Scenario | Expected |
|----------|----------|
| Boundary only, routes between boundary types | ✓ Pass (degenerate case) |

### Orphan Branch

| Scenario | Expected |
|----------|----------|
| Branch connected but no routes touch its types | ✗ Rule 1 + Rule 3 violations |

---

## Current Coverage

| Category | Tests | Status |
|----------|-------|--------|
| Rule 1 (inner produces) | 1 | ✓ |
| Rule 2 (route uniqueness) | 1 | ✓ |
| Rule 3 (inner consumes) | 0 | ❌ |
| Rule 4 (boundary OUT) | 1 | ✓ |
| Rule 5 (boundary IN) | 1 | ✓ |
| Entry uniqueness | 0 | ❌ |
| Builder state machine | 0 | ❌ |
| Multi-branch | 0 | ❌ |
| Edge cases | 0 | ❌ |

---

## Implementation Order

1. **Entry uniqueness** — Recently added logic, highest risk
2. **Rule 3** — Completes validation rule coverage
3. **Builder state machine** — Documents API contract
4. **Multi-branch linear** — Most common real scenario
5. **Edge cases** — Defensive coverage

---

## References

- [inner-covenants.md](inner-covenants.md) — Parent proposal
- [boundary-as-ports.md](boundary-as-ports.md) — Boundary design
