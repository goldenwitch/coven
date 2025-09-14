# RolloutMuxConsole

Minimal console app that demonstrates the new flexible mux pattern without DI:

- Reads Codex session JSONL from `<workspace>/.codex/log/codex.rollout.jsonl` and writes it to the console.
- Reads console input and forwards it to Codex process stdin.

Configuration:

- Edit the `Config` section at the top of `Program.cs`:
  - `ExecutablePath` — computed by OS:
    - Windows: `%AppData%\npm\codex.cmd`
    - Non-Windows: `codex` (resolved via PATH)
  - `WorkspaceDirectory` — set to a directory, or leave `null` to use the current directory.
  - `Debug` — set `true` to dump PATH entries on startup.

The console sets the following environment variables for Codex (see scratch notes):
- `CODEX_HOME=<workspace>/.codex`
- `CODEX_TUI_RECORD_SESSION=1`
- `CODEX_TUI_SESSION_LOG_PATH=<workspace>/.codex/log/codex.rollout.jsonl`

No extra CLI flags are passed; Codex inherits the working directory via the process `WorkingDirectory`.

Usage:

```bash
dotnet run --project Toys/Coven.Toys.RolloutMuxConsole/Coven.Toys.RolloutMuxConsole.csproj
```

Then type into the console; lines are sent to Codex stdin. Rollout lines from Codex are printed as they appear in the JSONL.
