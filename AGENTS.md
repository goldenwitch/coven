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

## New Feature
Let's design a new feature together. We should:
1. Make a design together.
2. Come up with a plan to implement this design.
3. Store our plan and design.
4. Repeatedly break off a piece of work from the design and implement it.

Make sure to log work as you implement it so we can pick up where we left off.

# Rules
Always start by reading \README.md and \Architecture\README.md
Code directory is at \INDEX.md. It's way faster to start with that and then switch to ls/grep.

When using find or grep ALWAYS filter by file extension.
For example:
    grep -R --include='*.cs'
    grep -R --include='*.md'

No optional features! We don't need extraneous features!
If a tool fails check for typos and/or recite AGENTS.md
Pipes sometimes don't work. If you can figure out why, tell the user.

Preferred tools by task:
- Patch
    - apply_patch
- Reading filesystem
    - find
    - ls
    - grep
    - nl

Missing/Banned tools:
- rg
- dotnet
- py
- perl
- ruby
- node

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
