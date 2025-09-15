# RolloutMuxConsole — Raw Key Passthrough (No PTY)

Status: Draft (design-first; scoped to key passthrough)

## Problem

`RolloutMuxConsole` currently forwards line-buffered input to the Codex subprocess, which prevents seamless interactive control (e.g., arrows, backspace, navigation). We want to pass through arbitrary keypresses as faithfully as possible without creating a PTY.

## Goals

- Stream keypresses immediately using a key pump (`Console.ReadKey(intercept: true)`).
- Send ordinary characters as typed; send special keys via ANSI/VT sequences.
- Support chords (Shift/Alt/Ctrl) for navigation keys and printable characters.
- Preserve existing behavior where possible and avoid breaking shared code.

## Non-Goals

- Full terminal emulation or PTY creation (ConPTY/posix PTY) in this phase.
- Guaranteed TTY semantics; some TUIs may still degrade when stdin/stdout are pipes.

## Key Mapping Strategy

- Printable characters:
  - If `ConsoleKeyInfo.KeyChar != '\0'`, write that character as-is (UTF-8), respecting Shift for casing/symbols.
  - Enter → `\r` (CR). Consider `\r\n` if the child expects CRLF.
  - Backspace → `\x08` (BS). Tab → `\t`. Escape → `\x1b`.

- Arrow/navigation keys (VT sequences):
  - Up/Down/Right/Left: `ESC [ A/B/C/D`.
  - Home/End: `ESC [ H/F` (or `ESC [ 1 ~` / `ESC [ 4 ~` if preferred).
  - Insert/Delete: `ESC [ 2 ~` / `ESC [ 3 ~`.
  - PageUp/PageDown: `ESC [ 5 ~` / `ESC [ 6 ~`.

- Function keys (best-effort xterm):
  - F1–F4: `ESC O P/Q/R/S`. F5–F12: `ESC [ 15~/17~/18~/19~/20~/21~/23~/24~`.

- Modifiers for navigation/function keys:
  - Use xterm modifier parameter: `ESC [ 1 ; m <code>` for cursor keys and many `~` sequences.
  - `m = 1 + (Shift?1:0) + (Alt?2:0) + (Ctrl?4:0)`.
    - Shift=2, Alt=3, Ctrl=5, Shift+Ctrl=6, Alt+Ctrl=7, Shift+Alt=4, All=8.
  - Examples: Ctrl+Up → `ESC[1;5A`, Shift+Left → `ESC[1;2D`, Alt+F4 → `ESC[1;3S` (where supported).

- Ctrl chords for letters:
  - If letter with Ctrl: map to control code `keyChar & 0x1F` (A→0x01 … Z→0x1A). Common controls: `Ctrl+C`→ETX(0x03), `Ctrl+Z`→SUB(0x1A), `Ctrl+[ `→ ESC.

- Alt chords for characters:
  - Prefix with ESC, then the base character (e.g., Alt+x → `ESC x`). Combine with Shift naturally via the char case.

## Escape Hatch Mode and Exit Strategy

- Keep default host behavior: `Ctrl+C` cancels the mux (host), as many terminals expect.
- Provide an escape hatch for sending control actions to the child without changing the host’s Ctrl+C:
  - Trigger: press backtick <code>`</code> to enter command mode (no input is sent to the child while composing).
  - UI: show a small prompt like `` `> `` and collect a line of text, then press Enter to execute.
  - Minimal commands (v1):
    - `exit` or `ctrlc`: send ETX (0x03, Ctrl+C) to the child process stdin.
    - `help`: print available commands locally.
    - `quit`: exit the mux (host shutdown).
  - Literal backtick: type double backtick ```` `` ```` in normal mode to send a single backtick to the child without entering command mode.
- EOF still exits: Windows `Ctrl+Z` then Enter; Unix `Ctrl+D` (when applicable). We will also continue to support host cancellation via `Ctrl+C`.

## Design

1) Add a non-breaking raw write API to `ProcessSendPort`:
   - `Task WriteAsync(string data, bool flush = true, CancellationToken ct = default)` — writes without newline.
   - Keep `WriteLineAsync` for any line-oriented usage.

2) Implement a key pump in the toy:
   - Use `Console.ReadKey(intercept: true)` in a loop; no local echo.
   - Translate `ConsoleKeyInfo` to a UTF-8 string per the mapping rules above.
   - Send via `send.WriteAsync` immediately for responsive behavior.
   - Escape hatch handling:
     - If a backtick is pressed:
       - If the next key is also a backtick, emit a single backtick to the child and continue in normal mode.
       - Otherwise, enter command mode, read a full command line (using `ReadLine`), execute locally, then return to normal mode.

3) Output help at startup:
   - “Raw key passthrough enabled. Ctrl+C passes to child; Ctrl+Break exits.”

4) Keep rollout tailing unchanged.

## Implementation Plan

1. Add `WriteAsync` to `ProcessSendPort` (additive; no breaking changes).
2. Add `KeyToSequence` helper in toy that maps `ConsoleKeyInfo` to a string.
3. Replace line-based input loop with the key pump (with cancellation, EOF handling, and escape hatch mode).
4. Update `README.md` usage and behavior notes.
5. Manual validation: verify printable, arrows (with modifiers), backspace, tab, enter; confirm Ctrl+C reaches child and Ctrl+Break exits host.

## Risks

- Some TUIs require a TTY and may degrade when attached to pipes.
- Function key and modifier sequences vary between terminals; we use common xterm-style sequences as a best effort.

## Future (Out of Scope)

- PTY backend (`--pty`) for true terminal semantics.
- Resize events and advanced terminal modes.
- Customizable keymaps and exit chords.

## Work Log

- 2025-09-15: Revised design to raw key passthrough and chord handling.
