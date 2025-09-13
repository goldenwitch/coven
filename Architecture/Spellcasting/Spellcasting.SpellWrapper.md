# Spell Interface Unification (Design)

This document proposes a single, shared interface that all spells implement. It keeps the Spellbook as the source of truth for names/schemas, simplifies the agent registration API, and enables a canonical, testable path for MCP integration without executable wrapper types.

## Goals

- Unify all spell shapes (zero, unary, binary) under a common non‑generic interface for metadata.
- Names/schemas come from Spellbook definitions; no duplicate sources.
- Keep invocation centralized and testable (via a single executor/registry), while avoiding per‑spell wrapper types.
- Decouple from MCP specifics while mapping cleanly to MCP tool listing/calling.

## Summary

- Introduce `ISpellContract` (non‑generic) that every spell implements.
- Existing spell interfaces (`ISpell`, `ISpell<TIn>`, `ISpell<TIn,TOut>`) extend `ISpellContract`.
- Agents accept `IReadOnlyList<ISpellContract>` during registration.

## Types

- ISpellContract
  - `SpellDefinition GetDefinition()` — source of truth for name and schemas.

- ISpell (zero)
  - Extends `ISpellContract`.
  - `Task CastSpell()`.
  - Default `GetDefinition()`: friendly name from spell type; no input/output schemas.

- ISpell<TIn> (unary)
  - Extends `ISpellContract`.
  - `Task CastSpell(TIn input)`.
  - Default `GetDefinition()`: friendly name for `TIn`; input schema from `TIn`.

- ISpell<TIn,TOut> (binary)
  - Extends `ISpellContract`.
  - `Task<TOut> CastSpell(TIn input)`.
  - Default `GetDefinition()`: friendly name for `TIn`; input/output schemas from `TIn`/`TOut`.

## Agent Integration

- Registration
  - `RegisterSpells(IReadOnlyList<ISpellContract> spells)`.
  - Agent builds:
    - Toolbelt: `spells → McpTool(GetDefinition().Name, InputSchema, OutputSchema)`.
    - Executor registry: a single implementation that maps `GetDefinition().Name` to the spell instance and knows how to invoke it.

## Testing Strategy

- Spell interfaces
  - Verify default `GetDefinition()` values for zero/unary/binary (friendly names and generated schemas).

- Executor registry
  - Name→spell mapping uses `GetDefinition().Name` only.
  - Deserialization and invocation for zero/unary/binary flows.
  - Result mapping: Always JSON

## Migration Plan

1) Add `ISpellContract` and have all spell interfaces extend it (add default `GetDefinition()` for `ISpell`).
2) Change agents’ `RegisterSpells` to accept `IReadOnlyList<ISpellContract>`.
3) Remove any per‑spell wrapper types and duplicate sources of truth for names/schemas.