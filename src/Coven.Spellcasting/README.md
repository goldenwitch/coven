# Coven.Spellcasting

Structured “tool” actions for agents and apps. Define spells with typed inputs/outputs, compose them into a spellbook, and optionally generate schemas.

## What’s Inside

- Contracts: `ISpell`, `ISpell<TIn>`, `ISpell<TIn,TOut>`, `ISpellContract`.
- Composition: `Spellbook`, `SpellbookBuilder`.
- Schema: `SchemaGen` for describing spell inputs/outputs.
- Definition: `SpellDefinition` (name, description, input/output types).

## Why use it?

- Make capabilities explicit and typed for agents/tools.
- Generate machine‑readable schemas for planning and validation.

## Minimal Example

```csharp
using Coven.Spellcasting;

public sealed class AddSpell : ISpell<AddInput, AddResult>
{
    public SpellDefinition Definition => new(
        Name: "add",
        Description: "Add two integers",
        InputType: typeof(AddInput),
        OutputType: typeof(AddResult));

    public Task<AddResult> Cast(AddInput input, CancellationToken ct = default)
        => Task.FromResult(new AddResult(input.A + input.B));
}

var book = new SpellbookBuilder()
    .AddSpell<AddInput, AddResult>(new AddSpell())
    .Build();

// Optional: schema for tool wiring
string jsonSchema = SchemaGen.Generate(book);
```

## See Also

- Architecture: Abstractions and Branches.
- Packages: `Coven.Agents` for agent orchestration.
