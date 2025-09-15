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
ALWAYS filter by file extension for grep when inspecting the src folder.
There is a lot of items and binaries so save yourself some pain by filtering them.
For example:
    grep -R --include='*.cs'
    grep -R --include='*.md'

The only command that is allowed for updating files is apply_patch

The following commands are examples of commands definitely available in the environment. If they fail it is because of some failure in calling them, not because they aren't available.
- pwd
- ls -la
- cat .editorconfig
- head -n 5 .editorconfig
- tail -n 5 .editorconfig
- sed -n '1,5p' .editorconfig
- wc -l .editorconfig
- cat .editorconfig | head -n 3
- head -n 3 .editorconfig | wc -l
- sed -n '1,3p' .editorconfig | head -n 2
- cat .editorconfig | echo piped
- grep -n "=" .editorconfig | head -n 2
- echo test | cat
- cat .editorconfig | grep "root"
- tail -n 10 .editorconfig | head -n 2
- nl -ba .editorconfig | head -n 3
- grep -n "AI" README.md | head -n 2
- cat README.md | echo piped-to-echo
- wc -c .editorconfig

The following commands definitely fail.
- awk 'NR<=5{print}' .editorconfig (auto-rejected)
- stat .editorconfig (auto-rejected)
- hexdump -C -n 64 .editorconfig (auto-rejected)
- file .editorconfig (auto-rejected)
- cmd.exe /c type .editorconfig (auto-rejected)
- powershell -NoProfile -Command Get-Content -TotalCount 5 .editorconfig (auto-rejected)
- findstr /N ".*" .editorconfig (auto-rejected)
- more .editorconfig (auto-rejected)
- cut -d"=" -f1 .editorconfig | head -n 3 (auto-rejected)
- tr -d "\r" < .editorconfig | head -n 3 (auto-rejected)
- dd if=.editorconfig bs=1 count=16 2>/dev/null (auto-rejected)
- dir (auto-rejected)
- rg (command not available)

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
