# RolloutMuxConsole

Minimal console app demonstrating a simple mux without DI:

- Tails Codex rollout JSONL from `<workspace>/.codex/log/codex.rollout.jsonl` to stdout.
- Forwards console input to Codex process stdin.
- Supports raw key passthrough (arrows, home/end, delete, page keys, tab, backspace, enter, ESC), plus an escape-hatch for control actions.

Behavior:

- Uses `codex` on `PATH` as the executable.
- Uses the current directory as the workspace.
- Sets the following environment variables:
  - `CODEX_HOME=<workspace>/.codex`
  - `CODEX_TUI_RECORD_SESSION=1`
  - `CODEX_TUI_SESSION_LOG_PATH=<workspace>/.codex/log/codex.rollout.jsonl`
- No extra CLI flags; Codex inherits the working directory.
- Starts Codex eagerly after wiring the rollout tail, so rollout begins without requiring initial input.

Interactive input:

- Raw keys are sent immediately; modifiers for arrows use xterm-style CSI sequences (e.g., `ESC[1;5A` for Ctrl+Up).
- Enter sends `Environment.NewLine` (platform default; CRLF on Windows, LF on Unix).
- Backtick escape-hatch:
  - Press backtick `` ` `` to enter command mode; type a command and press Enter.
  - Commands: `help`, `exit`/`ctrlc` (send Ctrl+C to child), `quit` (exit mux).
  - Type a double backtick `` `` `` to send a literal backtick to the child.
  - Press `Esc` while in command mode with no text entered to pass through a literal ESC to the child and exit command mode. If text has been entered, `Esc` cancels command entry without sending ESC.
- Ctrl+C exits the mux (host); use the escape-hatch `exit` to send Ctrl+C to the child.

Log handling:

- Never truncates an existing rollout log. If `<workspace>/.codex/log/codex.rollout.jsonl` already exists, a unique per-run file name is used instead, avoiding clobbering.
- On exit, if this app created the rollout log during the session, it is deleted. Empty `.codex/log` and `.codex` folders created by this app are removed.

Usage:

```bash
dotnet run --project Toys/Coven.Toys.RolloutMuxConsole/Coven.Toys.RolloutMuxConsole.csproj
```

Interact directly: keys are forwarded to the Codex stdin as you type. Rollout lines print as they appear in the JSONL. Use the backtick escape-hatch for control actions and quitting.
