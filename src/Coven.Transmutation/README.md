# Coven.Transmutation

Pure transformations between types used across Coven. Transmuters describe how one type becomes another, without side‑effects.

## What’s Inside

- ITransmuter<TIn,TOut>: one‑way, pure transformation. `TOut` must be non-null.
- IBiDirectionalTransmuter<TIn,TOut>: two‑way transformation (afferent/efferent directions). Both types must be non-null.
- IBatchTransmuter<TChunk,TOutput>: many‑to‑one transformation over a window of chunks. `TOutput` must be non-null.
- BatchTransmuteResult<TChunk,TOutput>: output plus optional remainder chunk.
- LambdaTransmuter<TIn,TOut>: adapter to build a transmuter from a delegate.
- IImbuingTransmuter<TIn,TReagent,TOut>: two‑parameter transmutation using an input plus a reagent. Also implements `ITransmuter<(TIn Input, TReagent Reagent), TOut>` for tuple‑based composition.

## Principles

- **Pure**: no observable side‑effects (no I/O, logging, mutation of external state).
- **Deterministic**: same inputs → same outputs.
- **Total**: transmuters always produce output—they transform, they don't filter. Use `where TOut : notnull` constraints.
- **Cancel‑aware**: accept and honor `CancellationToken` where work may be long‑running.
- **Exception‑transparent**: throw upstream; do not swallow or log.

### Filter Before Transmuting

Transmuters should not encode filtering logic (deciding whether to produce output at all). If you need to filter entries, apply the filter **before** invoking the transmuter:

```csharp
// ❌ Don't: return null to signal "skip this entry"
public Task<ResponseItem?> Transmute(Entry input, CancellationToken ct)
    => input switch
    {
        UserMessage u => Task.FromResult<ResponseItem?>(Map(u)),
        _ => Task.FromResult<ResponseItem?>(null)  // Bad: filtering inside transmuter
    };

// ✅ Do: filter at the call site, transmuter handles only valid inputs
await foreach (var entry in journal.ReadBackwardAsync(...))
{
    if (entry is UserMessage or AssistantMessage)  // Filter here
    {
        var item = await transmuter.Transmute(entry, ct);  // Transmuter always succeeds
        buffer.Add(item);
    }
}

// Transmuter throws for unexpected types
public Task<ResponseItem> Transmute(Entry input, CancellationToken ct)
    => input switch
    {
        UserMessage u => Task.FromResult(MapUser(u)),
        AssistantMessage a => Task.FromResult(MapAssistant(a)),
        _ => throw new ArgumentOutOfRangeException(nameof(input),
            $"Cannot transmute {input.GetType().Name}. Filter before transmuting.")
    };
```

This separation keeps transmuters as pure transforms while filtering remains explicit and testable at the boundary.

## One‑Way Transmutation

```csharp
using Coven.Transmutation;

// Simple pure mapping (e.g., DTO → domain)
public sealed class UserDtoToModel : ITransmuter<UserDto, User>
{
    public Task<User> Transmute(UserDto Input, CancellationToken ct = default)
        => Task.FromResult(new User(Input.Id, Input.Name.Trim()));
}
```

## Bidirectional Transmutation

```csharp
using Coven.Transmutation;

// Two pure mappings in opposite directions (afferent/efferent)
public sealed class UserBiMap : IBiDirectionalTransmuter<UserDto, User>
{
    public Task<User> TransmuteAfferent(UserDto Input, CancellationToken ct = default)
        => Task.FromResult(new User(Input.Id, Input.Name.Trim()));

    public Task<UserDto> TransmuteEfferent(User Output, CancellationToken ct = default)
        => Task.FromResult(new UserDto { Id = Output.Id, Name = Output.Name });
}
```

## Batch Transmutation (Many→One)

```csharp
using Coven.Transmutation;

// Concatenate chunk text into a single output
public sealed class TextBatch : IBatchTransmuter<MyChunk, MyOutput>
{
    public Task<BatchTransmuteResult<MyChunk, MyOutput>> Transmute(IEnumerable<MyChunk> input, CancellationToken ct = default)
    {
        string text = string.Concat(input.Select(c => c.Text));
        return Task.FromResult(new BatchTransmuteResult<MyChunk, MyOutput>(
            new MyOutput(text),
            HasRemainder: false,
            Remainder: default));
    }
}
```

Remainders are useful when only part of the last chunk is consumed; the unused tail returns as `Remainder` to seed the next window.

## Delegate Adapter

```csharp
var t = new LambdaTransmuter<int, string>((i, ct) => Task.FromResult(i.ToString()));
```

## Imbuing Transmutation (Input + Reagent)

```csharp
using Coven.Transmutation;

// Implement the two-parameter interface directly
public sealed class SumWithBiasImbuing : IImbuingTransmuter<int, int, int>
{
    public Task<int> Transmute(int Input, int Reagent, CancellationToken ct = default)
        => Task.FromResult(Input + Reagent);
}

// Use it directly with separate parameters
int result1 = await new SumWithBiasImbuing().Transmute(10, 5); // 15

// Because IImbuingTransmuter also implements ITransmuter<(int,int),int>,
// it can be used in tuple-based composition without extra adapters
ITransmuter<(int Input, int Bias), int> tupleTransmuter = new SumWithBiasImbuing();
int result2 = await tupleTransmuter.Transmute((10, 5)); // 15
```

## Tips

- Keep transmuters small and testable; stitch them together via DI.
- Prefer immutable inputs/outputs to reinforce purity.
- Separate policy decisions (window/shatter) from transmutation logic.

## See Also

- Architecture: Windowing and Shattering; Journaling and Scriveners.
- Packages using transmuters: `Coven.Chat`, `Coven.Agents`, `Coven.Agents.OpenAI`.
