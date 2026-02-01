# Coven.Core

Engine primitives for composing and running a Coven: MagikBlocks, Scriveners (journals), orchestration, and DI helpers to build a spine (ritual) of work.

## What’s Inside

- IMagikBlock<TIn,TOut>: unit of work with `DoMagik`.
- IScrivener<T>: append‑only, typed journal with tailing; includes `InMemoryScrivener<T>`.
- Builder: `BuildCoven`, `CovenServiceBuilder` to register blocks and finalize runtime.
- Orchestration: `ICoven` to invoke rituals; `IBoard` and pull mode options.
- CompositeDaemon<T>: base class for branches with encapsulated inner covenants.
- Utilities: `Empty` marker type; capability tags and selection strategy hooks.

## Why use it?

- Deterministic pipelines: execute a chain of typed blocks with explicit I/O.
- Decoupled comms: components communicate via journals instead of callbacks.
- Testable by design: swap `InMemoryScrivener<T>` and run blocks in isolation.

## Usage

```csharp
using Coven.Core;
using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;

// A minimal block that writes/reads journals or orchestrates other services
internal sealed class HelloBlock : IMagikBlock<Empty, Empty>
{
    public Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        // do work; start daemons; read/write journals
        return Task.FromResult(input);
    }
}

ServiceCollection services = new();

services.BuildCoven(c =>
{
    c.MagikBlock<Empty, Empty, HelloBlock>();
    c.Done();
});

ServiceProvider provider = services.BuildServiceProvider();
ICoven coven = provider.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
```

## Testing

- Prefer `InMemoryScrivener<T>` for journals and invoke blocks directly.
- Treat `OperationCanceledException` as cooperative shutdown when using tokens.

## See Also

- Architecture: Journaling and Scriveners; Windowing and Shattering.
- Persistence: `/src/Coven.Scriveners.FileScrivener/` (file‑backed snapshots for journals).
- Samples: `src/samples/01.DiscordAgent` for end‑to‑end orchestration.
