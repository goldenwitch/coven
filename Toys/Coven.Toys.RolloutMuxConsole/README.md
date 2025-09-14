# RolloutMuxConsole

Minimal console app that demonstrates the new flexible mux pattern without DI:

- Reads Codex rollout from `<workspace>/.codex/codex.rollout.jsonl` and writes it to the console.
- Reads console input and forwards it to Codex process stdin.

Configuration:

- Edit the `Config` section at the top of `Program.cs`:
  - `ExecutablePath` — computed by OS:
    - Windows: `%AppData%\npm\codex.cmd`
    - Non-Windows: `codex` (resolved via PATH)
  - `WorkspaceDirectory` — set to a directory, or leave `null` to use the current directory.
  - `Debug` — set `true` to dump PATH entries on startup.

Usage:

```bash
dotnet run --project Toys/Coven.Toys.RolloutMuxConsole/Coven.Toys.RolloutMuxConsole.csproj
```

Then type into the console; lines are sent to Codex stdin. Rollout lines from Codex are printed as they appear in the JSONL.
