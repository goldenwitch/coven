# Rules
Always start by reading \README.md and \Architecture\README.md
Code directory is at \INDEX.md. It's way faster to start with that and then switch to ls/grep.

Don't leave any explanation behind when removing an entire chunk of code. It defeats the purpose to add a line when we were trying to remove one.

Use these patterns to explore the repo efficiently within the CLI constraints. Avoid long, unscoped outputs.
- Output limit: Command output truncates at ~10 KB or 256 lines per command. Use chunking patterns below to page through results.
- Update policy: The only command allowed for writing files is `apply_patch`.

## Search (grep)
- Filter by extension: Always restrict recursive greps.
  - Pattern: `grep -R --include=*.EXT --exclude-dir=bin --exclude-dir=obj -n 'PATTERN' PATH`
- Do not quote long option values. Using quotes around `--include`/`--exclude-dir` values (e.g., `--include='*.cs'` or `--exclude-dir="obj"`) is rejected by the harness. Use unquoted forms: `--include=*.cs`, `--exclude-dir=obj`.
- Quoting the search pattern itself is fine: `grep -R --include=*.md -n 'Architecture Guide' .`
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

## Available commands
- grep
- sed
- head
- tail
- wc
- nl
- ls
- cat
- find
- apply_patch

## Unavailable commands
- rg
- dir
- pwsh
- powershell
- cmd
- python
- dotnet
- find -exec

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
