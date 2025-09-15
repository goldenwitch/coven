# RolloutMuxConsole

Minimal console app demonstrating a simple mux without DI:

- Tails Codex rollout JSONL from `<workspace>/.codex/log/codex.rollout.jsonl` to stdout.
- Forwards console input to Codex process stdin.
 - Lazily starts Codex only when the first line is sent to stdin.

Behavior:

- Uses `codex` on `PATH` as the executable.
- Uses the current directory as the workspace.
- Sets the following environment variables:
  - `CODEX_HOME=<workspace>/.codex`
  - `CODEX_TUI_RECORD_SESSION=1`
  - `CODEX_TUI_SESSION_LOG_PATH=<workspace>/.codex/log/codex.rollout.jsonl`
- No extra CLI flags; Codex inherits the working directory.

Log handling:

- Never truncates an existing rollout log. If `<workspace>/.codex/log/codex.rollout.jsonl` already exists, a unique per-run file name is used instead, avoiding clobbering.
- On exit, if this app created the rollout log during the session, it is deleted. Empty `.codex/log` and `.codex` folders created by this app are removed.

Usage:

```bash
dotnet run --project Toys/Coven.Toys.RolloutMuxConsole/Coven.Toys.RolloutMuxConsole.csproj
```

Type into the console; lines are sent to Codex stdin. Rollout lines print as they appear in the JSONL.
