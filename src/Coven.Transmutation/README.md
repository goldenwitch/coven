# Coven.Transmutation

Pure transformations between types used across Coven. Transmuters describe how one type becomes another, without side‑effects.

## What’s Inside

- ITransmuter<TIn,TOut>: one‑way, pure transformation.
- IBiDirectionalTransmuter<TIn,TOut>: two‑way transformation (afferent/efferent directions).
- IBatchTransmuter<TChunk,TOutput>: many‑to‑one transformation over a window of chunks.
- BatchTransmuteResult<TChunk,TOutput>: output plus optional remainder chunk.
- LambdaTransmuter<TIn,TOut>: adapter to build a transmuter from a delegate.

## Principles

- Pure: no observable side‑effects (no I/O, logging, mutation of external state).
- Deterministic: same inputs → same outputs.
- Cancel‑aware: accept and honor `CancellationToken` where work may be long‑running.
- Exception‑transparent: throw upstream; do not swallow or log.

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

## Tips

- Keep transmuters small and testable; stitch them together via DI.
- Prefer immutable inputs/outputs to reinforce purity.
- Separate policy decisions (window/shatter) from transmutation logic.

## See Also

- Architecture: Windowing and Shattering; Journaling and Scriveners.
- Packages using transmuters: `Coven.Chat`, `Coven.Agents`, `Coven.Agents.OpenAI`.
