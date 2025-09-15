# Process Send Port: Remove WriteLine; Use Write

Scope: Eliminate `WriteLineAsync` entirely across Coven. Replace the write contract with `WriteAsync` so the child process controls framing. Back-compat is NOT preserved in this refactor.

Status: Complete. Interfaces, implementations, and tests updated; line convenience implemented as an ITailMux extension. All tests pass.

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
- Processes decide framing: do not append newlines implicitly.
- Single write API: `WriteAsync(string, CancellationToken)` on send ports and muxes.
- Remove ambiguity: delete `WriteLineAsync` rather than deprecate.

## API Surface Changes
- [api] `ISendPort`: Replace `WriteLineAsync(string, CancellationToken)` with `WriteAsync(string, CancellationToken)`.
- [api] `ITailMux`: Replace `WriteLineAsync(string, CancellationToken)` with `WriteAsync(string, CancellationToken)`.
- [internal] `BaseCompositeTailMux`: forward `WriteAsync` to `SendPort.WriteAsync`.
- [internal] `InMemoryTailMux`: implement `WriteAsync` (remove `WriteLineAsync`).
- [internal] `ProcessSendPort`: remove `WriteLineAsync`; keep `WriteAsync` as the single write method.

### Line Convenience (Extension)
- [ok] Added a single extension (no interface changes):
  - `Task WriteLineAsync(this ITailMux mux, string data, CancellationToken ct = default)` → `mux.WriteAsync(data + Environment.NewLine, ct)`

## Migration Checklist
- Replace `WriteLineAsync` with `WriteAsync` on `ISendPort`, `ITailMux`, and all implementations.
- Update all call sites (agents, toys, samples) to use `WriteAsync`.
- Update test fixtures and contract tests to the new method name.
- Remove any remaining `WriteLineAsync` members and adjust docs accordingly.
- Provide ITailMux-based `WriteLineAsync` extension for callers that truly need line framing.

## Component Summary (post-refactor)
- Toys/Samples: No direct `WriteLineAsync` callers remain; RolloutMuxConsole already uses `WriteAsync`. [ok]
- Agents: `CodexCliAgent` ingress path uses `ITailMux.WriteLineAsync` extension to satisfy line-delimited stdin. [ok]
- Tests: All references migrated to `WriteAsync`. [tests]
- Extensions: Provide opt-in `WriteLineAsync` on ITailMux under `Coven.Spellcasting.Agents`. [docs]

## Reviews (by project)
- Toys
  - Coven.Toys.RolloutMuxConsole: [ok] Already on `WriteAsync`; no change.
  - Coven.Toys.MockProcess: [ok] No usage of ports/mux write API.
  - [ok] RolloutMuxConsole: Enter now maps to `Environment.NewLine` for cross-platform line submission.
- Samples
  - 01.LocalCodexCLI: [ok] No direct usage of send ports.
- Agents
  - Coven.Spellcasting.Agents.Codex: [api] `CodexCliAgent` ingress uses `ITailMux.WriteAsync`.
- Tests
  - Coven.Spellcasting.Agents.Tests: [tests] Contract and fixtures updated to `WriteAsync`.
  - Coven.Spellcasting.Agents.Codex.Tests: [tests] Contract and fixtures updated to `WriteAsync`; port tests remain valid using `WriteAsync`.
  - [ok] Mock process stdin uses a line reader; agent ingress uses `WriteLineAsync` extension to satisfy it.

## Detailed Change Log
- [api] Replace `WriteLineAsync` with `WriteAsync` in `ISendPort` and `ITailMux`.
- [internal] `BaseCompositeTailMux` forwards `WriteAsync` to send port.
- [internal] `InMemoryTailMux` implements `WriteAsync`; sink unchanged.
- [internal] Removed `WriteLineAsync` from `ProcessSendPort`.
- [ok] `CodexCliAgent` ingress now uses `ITailMux.WriteLineAsync` extension.
- [tests] Updated test adapters and all contract/port tests to use `WriteAsync`.
- [ok] Implemented `ITailMux.WriteLineAsync` extension for ergonomic line writes.
- [ok] RolloutMuxConsole Enter key emits `Environment.NewLine` instead of raw CR.

## Testing & Verification
- All solution tests pass locally post-refactor, including the end-to-end mock process test (`Agent_Writes_To_Process_And_Reads_Rollout`).
- Verified RolloutMuxConsole Enter mapping submits `Environment.NewLine` on all platforms.
- Manual audit confirmed no remaining interface-based `WriteLineAsync` members.

## Deferred / Follow-ups
- [docs] Update Architecture/README and any snippets referring to line writes.
- [cleanup] Run a final sweep for any stale mentions in comments.
- [ok] Implemented extension: `WriteLineAsync(this ITailMux, ...)`. No additional helpers.

## Anti-Patterns Avoided
- Forcing newline framing onto child processes.
- Interpreting raw key sequences as line-delimited prematurely.

## References
- Root README: Samples and Toys guidance.
- `src/Coven.Spellcasting.Agents/Tail/ProcessSendPort.cs` (supports `WriteAsync`).
