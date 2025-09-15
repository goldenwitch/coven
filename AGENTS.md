# Work Scopes
Ask the user which one of the following scopes you are working on and then follow those instructions with higher priority than any others.

If your role is Refactor then follow the ## Refactor section.
If your role is New Feature then follow the ## New Feature section.
## Refactor
Let's start at the top level projects like toys and samples and then work backwards eliminating optional features or
configuration based on whether they are needed.

The goal is to document items. Label each item you add with tags like "bug" or "redundant" or "unnecessary".
We will go through each project one at a time with the user, and decide which items to fix.

Use ls in the refactor/ folder and read the existing refactor docs to know where you left off.
This means that we have to update the refactor docs each time we implement one of the items.

When starting a new refactor document, copy `refactor/TEMPLATE.md` into a new file under `refactor/` (for example, `refactor/<topic>.md`) and follow the structure. Use the tag conventions from the template ([ok], [bug], [api], [internal], [tests], [docs], [cleanup], [redundant], [design]). The `refactor/cancellation-tokens.md` file is a good example of a completed document.

## New Feature
Let's design a new feature together. We should:
1. Make a design together.
2. Come up with a plan to implement this design.
3. Store our plan and design.
4. Repeatedly break off a piece of work from the design and implement it.

Make sure to log work as you implement it so we can pick up where we left off.

## Scrappy
We are focused on a single small fix. Let's keep changes scoped to this fix.

# Rules
Always start by reading \README.md and \Architecture\README.md
Code directory is at \INDEX.md. It's way faster to start with that and then switch to ls/grep.

## Commands

Use these patterns to explore the repo efficiently within the CLI constraints. Avoid long, unscoped outputs.

- Output limit: Command output truncates at ~10 KB or 256 lines per command. Use chunking patterns below to page through results.
- Update policy: The only command allowed for writing files is `apply_patch`.
- Start points: Always read `\README.md` and `\Architecture\README.md` first. The code index is at `\INDEX.md`.

### Search (grep)
- Filter by extension: Always restrict recursive greps.
  - Pattern: `grep -R --include=*.EXT --exclude-dir=bin --exclude-dir=obj -n 'PATTERN' PATH`
- Page large results: Use head/tail chunking to stay under the output limit.
  - First chunk: `... | head -n 200`
  - Next chunk(s): `... | tail -n +201 | head -n 200`
- Cap per-file noise: Add `-m N` to limit matches per file.
- Find files first: `-l` to list matching files, then grep per file for details.
- Add context: `-C 2` to include 2 lines of context around matches.
- Quiet checks: `-q` to test existence (exit code) without printing lines.

Recommended scopes (substitute placeholders):
- Code: `grep -R --include=*.cs --exclude-dir=bin --exclude-dir=obj -n 'PATTERN' .`
- Markdown: `grep -R --include=*.md -n 'PATTERN' .`
- Projects/Solutions: `grep -R --include=*.csproj -n '<TargetFramework' .` and `grep -R --include=*.sln -n 'Project(' .`

Grep quirk in this harness:
- Do not quote long option values. Using quotes around `--include`/`--exclude-dir` values (e.g., `--include='*.cs'` or `--exclude-dir="obj"`) is rejected by the harness. Use unquoted forms: `--include=*.cs`, `--exclude-dir=obj`.
- Quoting the search pattern itself is fine: `grep -R --include=*.md -n 'Architecture Guide' .`

### Read files in chunks
- First lines: `head -n 200 FILE`
- Last lines: `tail -n 200 FILE`
- Specific range: `sed -n 'START,ENDp' FILE` (e.g., `sed -n '1,200p' FILE`)

### Known constraints
- Not available: `rg`, `dir`, or platform-specific binaries like `cmd.exe`, `powershell`, `stat`, `file`, `more` (auto-rejected in this environment).
- Prefer portable POSIX utilities: `grep`, `sed`, `head`, `tail`, `wc`, `nl`, `ls`, `cat`.
- Avoid `find -exec` in this harness; it may be blocked. Prefer listing files with `find` or `ls`, then open with `head`/`tail`/`sed -n`.

## Usings: Acceptable Usage
- No fully qualified types or members: avoid `Namespace.Type.Member` in code; add a `using` or alias instead.
- Prefer aliasing on conflicts: when two types share a name, create an alias rather than using fully qualified names.
- Don’t add usings covered by implicit usings (e.g., `System`, `System.Threading.Tasks`).

Pair 1 — Minimal usings
Good
```csharp
// Only what’s needed; rely on implicit usings
using Coven.Chat; // for ChatEntry/ChatThought

var entry = new ChatThought("console", "hello");
```

Pair 2 — No fully qualified types/members
Good
```csharp
using Coven.Chat.Adapter.Console.Di;

services.AddConsoleChatAdapter(o => o.InputSender = "console");
```
