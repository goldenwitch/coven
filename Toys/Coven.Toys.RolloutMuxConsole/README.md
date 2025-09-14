# RolloutMuxConsole

Minimal console app that demonstrates the new flexible mux pattern without DI:

- Reads Codex rollout from `<workspace>/.codex/codex.rollout.jsonl` and writes it to the console.
- Reads console input and forwards it to Codex process stdin.

Environment variables:

- `CODEX_EXE` — path to `codex` executable (default: `codex` on PATH)
- `CODEX_WORKSPACE` — workspace directory (default: current directory)

Usage:

```bash
dotnet run --project Toys/Coven.Toys.RolloutMuxConsole/Coven.Toys.RolloutMuxConsole.csproj
```

Then type into the console; lines are sent to Codex stdin. Rollout lines from Codex are printed as they appear in the JSONL.

