# Coven.Spellcasting.Spells

Spells represent a tool call that the agent can intentionally invoke. They are implemented as well-typed .NET classes, unified under a single, shared contract for metadata.

## Unified Contract
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