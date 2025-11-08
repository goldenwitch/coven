# Coven.Daemonology

Long‑running background services (daemons) with a small, testable surface. Provides a status contract so orchestration code can deterministically start, monitor, and shut down components.

## What’s Inside

- IDaemon: minimal lifecycle contract (Start/Shutdown, Status)
- ContractDaemon: base class that fulfills status/failure promises via a journal
- Status: lifecycle states (Stopped, Running, Completed)
- DaemonEvent: internal event records written for status/failure changes

## Why use it?

- Deterministic startup/shutdown: orchestration can wait for a specific status.
- Failure propagation: surface first failure for coordinated recovery.
- Testable: status/failure are journaled; behavior is unit‑testable.
- Side‑effect‑aware: follows repo guidelines for cancellation and disposal.

## Key Types

- IDaemon
  - `Status Status { get; }`
  - `Task Start(CancellationToken cancellationToken = default)`
  - `Task Shutdown(CancellationToken cancellationToken = default)`

- ContractDaemon
  - Derive to implement your own daemon.
  - Requires an `IScrivener<DaemonEvent>` to journal lifecycle events.
  - Protected helpers for correctness:
    - `Transition(Status newStatus, CancellationToken)` — write a status change and update `Status` atomically.
    - `Fail(Exception error, CancellationToken)` — write first failure for observers.
  - Public observers for orchestration:
    - `Task WaitFor(Status target, CancellationToken)` — completes on first matching status change.
    - `Task<Exception> WaitForFailure(CancellationToken)` — completes on first failure.

- Status
  - `Stopped`: not yet started or fully stopped.
  - `Running`: actively processing.
  - `Completed`: shut down successfully and cannot be restarted.

## Lifecycle Contract

Implementors should:

- Call `Transition(Status.Running)` near the end of Start once work is ready.
- Call `Transition(Status.Completed)` in Shutdown after cooperative cancellation and cleanup.
- Call `Fail(ex)` from catch blocks when unrecoverable errors occur.
- Honor the provided cancellation token and prefer linked tokens.
- Never restart after `Completed` (enforced by base class).

The base class guarantees thread‑safe state changes and fulfills any outstanding waits by writing events to the daemon event journal.

## Example: Minimal Custom Daemon

```csharp
using Coven.Core;
using Coven.Daemonology;

internal sealed class MyDaemon(IScrivener<DaemonEvent> events) : ContractDaemon(events)
{
    private CancellationTokenSource? _linked;
    private Task? _pump;

    public override async Task Start(CancellationToken cancellationToken = default)
    {
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pump = Task.Run(() => RunAsync(_linked.Token), _linked.Token);
        await Transition(Status.Running, cancellationToken);
    }

    public override async Task Shutdown(CancellationToken cancellationToken = default)
    {
        _linked?.Cancel();
        if (_pump is not null)
        {
            try { await _pump.ConfigureAwait(false); } catch (OperationCanceledException) { }
            _pump = null;
        }
        await Transition(Status.Completed, cancellationToken);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // do work
                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException) { /* cooperative */ }
        catch (Exception ex) { await Fail(ex, ct); }
    }
}
```

## DI and Orchestration

- Provide a journal for daemon events (most integrations call `TryAddSingleton<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>`).
- Register your daemon(s) as `ContractDaemon` so orchestration can enumerate them.
- Start daemons inside a MagikBlock and optionally wait for `Running` before proceeding.

Sample 01 pattern (Discord Agent) starts all daemons injected via DI:

```csharp
using Coven.Agents;
using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

internal sealed class RouterBlock(
    IEnumerable<ContractDaemon> daemons,
    IScrivener<ChatEntry> chat,
    IScrivener<AgentEntry> agents) : IMagikBlock<Empty, Empty>
{
    private readonly IEnumerable<ContractDaemon> _daemons = daemons ?? throw new ArgumentNullException(nameof(daemons));
    private readonly IScrivener<ChatEntry> _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    private readonly IScrivener<AgentEntry> _agents = agents ?? throw new ArgumentNullException(nameof(agents));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        foreach (ContractDaemon d in _daemons)
        {
            await d.Start(cancellationToken).ConfigureAwait(false);
            // Optionally: await d.WaitFor(Status.Running, cancellationToken);
        }

        // bridge chat ↔ agent work here ...
        return input;
    }
}
```

Integrations such as `Coven.Chat.Discord`, `Coven.Chat.Console`, `Coven.Agents.OpenAI`, and `Coven.Core.Streaming` register their own `ContractDaemon` implementations in DI. Consuming apps typically don’t construct daemons directly.

## Common Patterns and Tips

- Cancellation: always use linked tokens inside Start; pass tokens to awaited calls.
- Failure: surface first failure via `Fail(ex)`; orchestration can `await WaitForFailure()` if desired.
- Completion: use a unified Shutdown path; dispose managed resources there, then `Transition(Completed)`.
- No restarts: `Completed` is terminal; attempting to Start again throws.
- Journaling: ensure a single `IScrivener<DaemonEvent>` instance is available for the current scope.

## Referenced Samples

- Sample 01 — Discord Agent: `src/samples/01.DiscordAgent` uses multiple daemons (Discord chat, OpenAI agent, stream windowing) and starts them from a RouterBlock.

## Testing

Because status and failures are journaled, behaviors are easy to test with `InMemoryScrivener<DaemonEvent>`.

See tests under `src/Coven.Daemonology.Tests` for examples like:

- waiting for `Status.Running` after `Start()`
- waiting for `Status.Completed` after `Shutdown()`
- asserting that a completed daemon cannot restart
- propagating the first failure via `WaitForFailure()`

## See Also

- Root README: high‑level concepts (MagikBlocks, Scriveners, Window/Shatter)
- Architecture Guide: cancellation token guidance and cross‑cutting standards
