# Cancellation Token Refactor

Scope: Standardize CancellationToken usage across the solution. This document is the reference template for future Refactor agents: what to change, how to log it, and how to validate at scale.

Status: Complete (code and tests). Remaining: minor docs hardening only.

## Conventions
- Tags:
  - [ok]: Reviewed and aligned
  - [bug]: Functional defect fixed
  - [api]: Public API change
  - [internal]: Internal behavior/implementation detail
  - [tests]: Test-only change
  - [docs]: Documentation work
  - [cleanup]: Code quality/non-functional cleanup
  - [redundant]: Unnecessary pattern removed/avoided
  - [design]: Open design question

## Principles (adopted)
- Propagation: Always pass a CancellationToken down call chains; avoid CancellationToken.None.
- API shape: Prefer a single async method with `CancellationToken cancellationToken = default` as the last parameter when meaningful.
- Background services: Use and forward the provided stoppingToken.
- Linked tokens: Link only to compose multiple sources or apply an internal timeout bound; dispose the CTS.
- I/O: Prefer token-aware APIs; avoid WaitAsync if a better token overload exists.
- Exceptions: Do not wrap OperationCanceledException; do not log it as an error.
- Builder/Blocks: Implement `IMagikBlock<TIn,TOut>.DoMagik(TIn, CancellationToken)`. Builder lambdas use `Func<TIn, CancellationToken, Task<TOut>>`.
- Push & Pull: Push pipelines and Pull steps accept and flow tokens end-to-end.

## API Surface Changes
- [api] IMagikBlock<T,TOut>.DoMagik(T, CancellationToken)
- [api] IMagikBuilder<T,TOut>.MagikBlock<TIn,TOut>(Func<TIn, CancellationToken, Task<TOut>> func, IEnumerable<string>? capabilities = null)
- [api] ICovenAgent<TMessage>.RegisterSpells(IReadOnlyList<ISpellContract> spells, CancellationToken ct = default)
- [api] IBoard.PostWork<T,TOut>(..., CancellationToken) and IBoard.GetWork<TIn>(..., CancellationToken)
- [api] GetWorkRequest<TIn>(..., CancellationToken)
- [ok] ICoven.Ritual<...>(..., CancellationToken) overloads exist; callers now consistently pass tokens

## Migration Checklist
- Update all blocks to new `DoMagik(T, CancellationToken)` signature.
- Update builder lambdas to `(input, cancellationToken) => ...` form.
- Update agents to use `RegisterSpells(..., CancellationToken)`.
- Ensure orchestrations call `Ritual(..., cancellationToken)` and forward tokens to all awaited operations.
- Replace WaitAsync wrappers where native token-aware I/O is available.
- Remove CancellationToken.None and avoid swallowing OperationCanceledException.

## Component Summary (post-refactor)

### Core (Board/Builder/Pipeline)
- [internal] Push pipelines compiled as `Func<T, CancellationToken, Task<TOut>>`.
- [internal] Pull path threads tokens via GetWorkRequest<TIn>, Board.GetWorkPullAsync, RegisteredBlock.InvokePull, PullOrchestrator.
- [ok] ICoven.Ritual overloads accept tokens; DI scope semantics preserved.

### Agents
- [api] ICovenAgent.RegisterSpells(..., CancellationToken) implemented across agents; Wizards pass tokens to RegisterSpells and InvokeAgent.

### Toys/Samples
- [fixed] MockProcess + RolloutMuxConsole: cancel-aware stdin reader; tail/input tasks propagate tokens.
- [ok] ConsoleEcho/CodexConsole orchestrators propagate tokens.
- [ok] ConsoleAgentChat respects optional token and disposes linked CTS.

### Chat Infrastructure
- [ok] IScrivener, IAdapter, IAdapterHost already token-aware; usages validated.

## Reviews (by project)

### Toys
- Coven.Toys.ConsoleAgentChat
  - [ok] Optional token respected; linked CTS disposed. [redundant] Consider narrowing exception swallowing in CloseAgent() in a future pass.
- Coven.Toys.CodexConsole
  - [ok] `_coven.Ritual<Empty>(stoppingToken)`; adapter host/shutdown token-aware.
- Coven.Toys.ConsoleEcho
  - [ok] Propagates stoppingToken; OCE used for cooperative cancellation.
- Coven.Toys.RolloutMuxConsole
  - [ok] Ctrl+C -> CTS; token-aware stdin reader; tail/input tasks respect tokens.
- Coven.Toys.MockProcess
  - [fixed] stdin reader uses StreamReader.ReadAsync(..., CancellationToken); acceptable that some writes lack token overloads.

### Samples (01.LocalCodexCLI)
- OrchestratorService: [ok] `_coven.Ritual<Empty>(stoppingToken)`.
- Wizard: [ok] `RegisterSpells` passes token; `InvokeAgent` token-aware.
- Program: [ok] lifecycle via Ctrl+C; services accept tokens.

## Detailed Change Log
- [fixed] Toys.MockProcess.MockProcessOrchestrator: token-aware stdin reader (no WaitAsync).
- [fixed] Toys.RolloutMuxConsole.Program: token-aware stdin reader; tail/input tasks propagate tokens.
- [standardize] Toys.ConsoleAgentChat.ConsoleToyAgent: optional token respected; linked CTS scoped and disposed.
- [api] ICoven: token parameters on Ritual methods; call sites pass stoppingToken.
- [api] Magik pipeline: IMagikBlock.DoMagik and MagikUser accept tokens; Wizards propagate tokens to agents.
- [internal] Pipeline: compiled delegates accept/pass CancellationToken; invoker forwards to DoMagik.
- [bug][pull] Pull CT propagation implemented across GetWorkRequest<TIn>, IBoard.GetWork<TIn>, Board.GetWorkPullAsync, RegisteredBlock.InvokePull, PullOrchestrator.
- [api] Agents: optional token added to ICovenAgent.RegisterSpells; CodexCliAgent, toys/samples updated to pass/accept token.
- [bug][tests] Builder func overload standardized to `Func<TIn, CancellationToken, Task<TOut>>`; tests updated.
- [cleanup][redundant] Removed unnecessary `using System.Threading;` where implicit usings cover it.

## Testing & Verification
- All tests pass across the solution post-refactor.
- Coverage focuses on builder lambda shape, agent registration, and cancel-aware I/O loops.
- Manual audit confirmed no remaining CancellationToken.None in pipelines/pull wrappers.

## Deferred / Follow-ups
- [docs] Add a short “Cancellation” note to agent-specific docs (e.g., Codex agent page) to reinforce patterns.
- [design] Consider analyzer guidance to flag CancellationToken.None and enforce token-last parameters.

## Anti-Patterns Avoided
- Passing CancellationToken.None in internal flows (esp. pull wrappers).
- Using WaitAsync when a native token-aware API exists.
- Logging OperationCanceledException as an error.
- Swallowing broad exceptions during cooperative shutdown.

## References
- Root README: Cancellation subsection.
- Architecture/README: Cancellation Tokens section.
- Architecture/Board: Cancellation section.

