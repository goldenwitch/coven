# Spells

Spells represent a tool call that the agent can intentionally invoke. They are implemented as well-typed .NET classes, unified under a single, shared contract for metadata.

## Unified Contract
- ISpellContract: Non-generic base interface implemented by all spells. Exposes a readonly `SpellDefinition Definition { get; }`, the canonical source of name and JSON schemas.
- ISpell forms:
  - `ISpell` (zero-arg): `Task CastSpell()`.
  - `ISpell<TIn>` (unary): `Task CastSpell(TIn input)`.
  - `ISpell<TIn,TOut>` (binary): `Task<TOut> CastSpell(TIn input)`.
  - Defaults: The n-ary spell interfaces provide default `Definition` values:
    - Zero: friendly name from the spell type; no schemas.
    - Unary: friendly name + input schema from `TIn`.
    - Binary: friendly name + input schema from `TIn`, output schema from `TOut`.
  - Spellbooks remain the source of truth and may override names/schemas supplied to agents.

## Spell Registration
Because spells can be cast from outside a C# context, their shapes (name + JSON schemas) must be available:
- The Spellbook is the source of truth for names and schemas. It supplies an `IReadOnlyList<ISpellContract>` to agents.
- Schema generation occurs during Coven finalization (`.Done()`), ensuring deterministic names/schemas.

## DI
Spells are DI-friendly. Invocation constructs spells via the container so all dependencies are satisfied at call time.

## Agent Wire-up
Agents need to list and invoke tools. Two paths are supported:
1. MCP: Agent builds an MCP toolbelt from `Definition` for each `ISpellContract`. Invocation is centralized through an executor registry that maps `Definition.Name` → spell instance and reflects the appropriate call (zero/unary/binary).
2. Direct: For agents that support direct tool calls, names map to registered spells by `Definition.Name`.

## Spellbooks
A Spellbook represents the set of spells available to a `MagikUser`.

It contains:
1. The list of spells (as `ISpellContract`).
2. The canonical schemas for those spells.
3. Agent guidance describing how and when to use them.
