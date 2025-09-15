# Cancellation Token Strategy

Scope: Standardize `CancellationToken` usage across the solution. We ignore project boundaries and back-compat during refactor.

## Baseline Decisions (Aligned with guidance in scratch.txt)
- Propagation: Always pass a `CancellationToken` down call chains; avoid `CancellationToken.None`.
- Public API shape: Prefer a single method with an optional token parameter: `CancellationToken cancellationToken = default` as the last parameter. Only take a token if cancellation is meaningful.
- Background services: Use the provided `stoppingToken` and forward it to all started tasks and awaited operations.
- Linked tokens: Create linked CTS only to compose multiple sources or apply an internal upper bound; dispose the CTS.
- Cancellation-friendly I/O: Prefer APIs that accept a token. Where none exists, use `WaitAsync(cancellationToken)` around the awaiting operation.
- Exceptions: Use `cancellationToken.ThrowIfCancellationRequested()` for cooperative checks; never wrap `OperationCanceledException` and don’t log cancellation as an error.

## Toys Review

### Coven.Toys.ConsoleAgentChat
- ConsoleToyAgent.cs
  - [ok] Optional token respected; uses a linked CTS for internal cancel and disposes it. [unnecessary] Swallowing all exceptions in `CloseAgent()` is broader than needed; consider catching only `ObjectDisposedException` if we keep this pattern.
- ChatOrchestrator.cs
  - [ok] Passes `stoppingToken` to `_coven.Ritual<Empty>(stoppingToken)`.

### Coven.Toys.CodexConsole
- CodexOrchestrator.cs
  - [ok] Calls `_coven.Ritual<Empty>(stoppingToken)` and passes `stoppingToken` throughout; adapter host and shutdown wait are token-aware.

### Coven.Toys.ConsoleEcho
- EchoOrchestrator.cs
  - [ok] Propagates `stoppingToken` to adapter host and scrivener calls; cancellation handled via `OperationCanceledException`.

### Coven.Toys.RolloutMuxConsole
- Program.cs
  - [ok] Uses `CancellationTokenSource` with Ctrl+C; token-aware stdin reader via `StdInLineReader.ReadLineAsync` (no `WaitAsync`); tail/input tasks respect cancellation.

### Coven.Toys.MockProcess
- MockProcessOrchestrator.cs
  - [fixed] Replaced `Console.In.ReadLineAsync().WaitAsync(stoppingToken)` with a token-aware stdin reader based on `StreamReader.ReadAsync(Memory<char>, CancellationToken)` to avoid `WaitAsync` and support cooperative cancellation.
  - [note] `WriteJsonLineAsync` and `FlushAsync` have no token overloads; acceptable.
- LambdaTailMux.cs
  - [ok] Methods use optional tokens (e.g., `TailAsync(..., CancellationToken cancellationToken = default)`, `WriteLineAsync(..., cancellationToken = default)`). Keep optional-token shape per guidance; ensure tokens are propagated and honored. No interface change required.

## Samples Review

### 01.LocalCodexCLI
- OrchestratorService.cs (SampleOrchestrator)
  - [ok] Calls `_coven.Ritual<Empty>(stoppingToken)`; adapter host uses `stoppingToken`.
- Wizard.cs
  - [api][requires-upstream-change] `ICovenAgent.RegisterSpells(...)` has no token; add an optional token parameter and pass it.
  - [ok] `_agent.InvokeAgent(cancellationToken)` is token-aware and already propagated from `MagikUser`.
- Program.cs
  - [ok] Host lifecycle handles Ctrl+C; `RunAsync()` is fine. Ensure services/agents accept and use tokens once signatures change.

## Cross-Cutting Gaps
- [ok] `ICoven.Ritual<...>` and Magik pipeline expose and propagate optional tokens end-to-end.
- [ok] `IScrivener`, `IAdapter`, and `IAdapterHost` expose optional tokens and pass them through.
- [done] `ICovenAgent.RegisterSpells` accepts an optional token; call sites updated.
- [fixed] Pull mode CT propagation implemented; tokens passed end-to-end; no `CancellationToken.None` in pull wrappers.

## Implemented Changes (tracking)
- [fixed] Toys.MockProcess.MockProcessOrchestrator: token-aware stdin reader (no `WaitAsync`).
- [fixed] Toys.RolloutMuxConsole.Program: token-aware stdin reader (no `WaitAsync`).
- [standardize] Toys.ConsoleAgentChat.ConsoleToyAgent: optional token respected; linked CTS used only for internal cancel and disposed.
- [api] ICoven: optional-token parameters on all `Ritual` methods; call sites updated to pass `stoppingToken`.
- [api] Magik pipeline: `IMagikBlock.DoMagik` and `MagikUser` accept optional tokens; Wizards propagate tokens to agents.
- [internal] Board + Pipeline: compiled delegates accept and pass `CancellationToken`; invoker passes tokens to `DoMagik`.
- [bug][pull] Pull mode CT propagation: added `CancellationToken` to `GetWorkRequest<TIn>` and `IBoard.GetWork<TIn>`; threaded token through `Board.GetWorkPullAsync`, `RegisteredBlock.InvokePull`, and `PullOrchestrator.Run/NextAsync`; removed `CancellationToken.None` usage in `BlockInvokerFactory.CreatePull` and pull wrappers; `Board.PostWork` now forwards the token to pull orchestrator.
- [api] Agents: added optional token to `ICovenAgent.RegisterSpells`; updated `CodexCliAgent`, `ConsoleToyAgent`, and sample/toy wizards to pass/accept the token.
 - [bug][tests] Fixed builder func overload mismatch: `IMagikBuilder.MagikBlock` now takes `Func<TIn, CancellationToken, Task<TOut>>`; updated tests to pass tokens in lambdas.
 - [cleanup][redundant] Removed unnecessary `using System.Threading;` usings where implicit usings cover it.

## Deferred (large-scope)
- [pending][api] Add optional token to `ICovenAgent.RegisterSpells` and ripple through agent implementations.
  - [done] Implemented.

### Pull Mode CT Propagation (status)
- [done] Completed all items in this section; tests updated and passing. Docs refresh remains.

## Next Task
- Pull mode:
  - [docs] Update Architecture docs to reflect pull-mode CT propagation and token flow.
- Agents API:
  - [docs] Document that `RegisterSpells` accepts an optional token across agents.
- Docs:
  - Add brief guidance in README/Architecture on choosing token-aware APIs and avoiding `CancellationToken.None`.

## Proposed Refactor Steps
1) Add optional `CancellationToken` parameters to remaining public interfaces (agents, pull mode) and implementations.
2) Update call sites in Toys/Samples to pass tokens; validate behavior.
3) Replace blocking I/O with cancel-aware patterns where native cancellation is missing.
4) Keep optional token parameters on public APIs per guidance; avoid redundant overloads.
5) Validate services shut down promptly and don’t log cooperative cancellation as errors.

## Open Questions
- Should `CloseAgent()` be replaced with token-driven shutdown only? [design]
- Are there any long-running operations without async APIs requiring additional cancellation patterns? [audit]
