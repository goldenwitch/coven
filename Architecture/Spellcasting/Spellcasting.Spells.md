# Spells

Spells represent a tool call that the agent can intentionally invoke. They are implemented as well-typed .NET classes, unified under a single, shared contract for metadata.

## Unified Contract
- ISpellContract: Non-generic base interface implemented by all spells. Exposes `SpellDefinition GetDefinition()` which is the canonical source of name and JSON schemas.
- ISpell forms:
  - `ISpell` (zero-arg): `Task CastSpell()`; default `GetDefinition()` uses the spell type’s friendly name; no input/output schema.
  - `ISpell<TIn>` (unary): `Task CastSpell(TIn input)`; default `GetDefinition()` provides friendly name and input schema from `TIn`.
  - `ISpell<TIn,TOut>` (binary): `Task<TOut> CastSpell(TIn input)`; default `GetDefinition()` provides friendly name and schemas from `TIn`/`TOut`.

## Spell Registration
Because spells can be cast from outside a C# context, their shapes (name + JSON schemas) must be available:
- The Spellbook is the source of truth for names and schemas. It supplies an `IReadOnlyList<ISpellContract>` to agents.
- Schema generation occurs during Coven finalization (`.Done()`), ensuring deterministic names/schemas.

## DI
Spells are DI-friendly. Invocation constructs spells via the container so all dependencies are satisfied at call time.

## Agent Wire-up
Agents need to list and invoke tools. Two paths are supported:
1. MCP: Agent builds an MCP toolbelt from `GetDefinition()` for each `ISpellContract`. Invocation is centralized through an executor registry that maps `GetDefinition().Name` → spell instance and reflects the appropriate call (zero/unary/binary).
2. Direct: For agents that support direct tool calls, names map to registered spells by `GetDefinition().Name`.

## Spellbooks
A Spellbook represents the set of spells available to a `MagikUser`.

It contains:
1. The list of spells (as `ISpellContract`).
2. The canonical schemas for those spells.
3. Agent guidance describing how and when to use them.