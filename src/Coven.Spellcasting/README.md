# Coven.Spellcasting

Structured "tool" actions for agents and apps. Define spells with typed inputs/outputs, compose them into a spellbook, and optionally generate schemas.

> **Note**: This library provides foundational infrastructure for tool/function definitions. Integration with agent orchestration (e.g., `Coven.Agents.OpenAI`) is planned but not yet implemented. Contributions welcome!

## What's Inside

- Contracts: `ISpell`, `ISpell<TIn>`, `ISpell<TIn,TOut>`, `ISpellContract`.
- Composition: `Spellbook`, `SpellbookBuilder`.
- Schema: `SchemaGen` for describing spell inputs/outputs.
- Definition: `SpellDefinition` record (name, optional input/output schemas).

## Why use it?

- Make capabilities explicit and typed for agents/tools.
- Generate machine-readable schemas for planning and validation.

## Minimal Example

```csharp
using Coven.Spellcasting;

public record AddInput(int A, int B);
public record AddResult(int Sum);

public sealed class AddSpell : ISpell<AddInput, AddResult>
{
    // Definition is auto-generated from TIn/TOut via default interface implementation
    // Name defaults to "AddInput", schemas generated from the record types

    public Task<AddResult> CastSpell(AddInput input)
        => Task.FromResult(new AddResult(input.A + input.B));
}

// Build a spellbook containing your spells
var book = new SpellbookBuilder()
    .AddSpell<AddInput, AddResult>(new AddSpell())
    .Build();

// Access spell definitions for tool registration
SpellDefinition def = book.Get<AddInput, AddResult>().Definition;
// def.Name == "AddInput"
// def.InputSchema == JSON schema for AddInput
// def.OutputSchema == JSON schema for AddResult
```

## SpellDefinition Record

```csharp
public record SpellDefinition(
    string Name,
    string? InputSchema = null,
    string? OutputSchema = null);
```

The `ISpell<TIn, TOut>` interface provides a default implementation that auto-generates the definition from the type parameters using `SchemaGen`.

## See Also

- Architecture: Abstractions and Branches.
- Packages: `Coven.Agents` for agent orchestration.
