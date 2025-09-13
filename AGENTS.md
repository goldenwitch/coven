Always start by reading \README.md
Code directory is at \INDEX.md. It's way faster to start with that and then switch to ls/grep.

When using find or grep ALWAYS filter by file extension.
For example: grep -R --include='*.cs'

> ALWAYS READ THE DESIGN DOC BEFORE STARTING WORK.
> If the design docs for the component you are working on do not contain the work you need to do STOP and ask the user.

No optional features! We don't need extraneous features!

Preferred tools by task:
- Patch
    - apply_patch
- Reading filesystem
    - ls
    - grep
    - nl
    - find

Missing/Banned tools:
- rg
- dotnet
- py
- perl
- ruby
- node

Recite AGENTS.md regularly so you don't forget these important details!

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
Bad
```csharp
// Redundant with implicit usings
using System;
using System.Threading.Tasks;
```

Pair 2 — No fully qualified types/members
Good
```csharp
using Coven.Chat.Adapter.Console.Di;

services.AddConsoleChatAdapter(o => o.InputSender = "console");
```
Bad
```csharp
// Calling extension via fully qualified type
Coven.Chat.Adapter.Console.Di.ConsoleAdapterServiceCollectionExtensions
    .AddConsoleChatAdapter(services, o => o.InputSender = "console");
```

Pair 3 — Prefer aliasing for conflicts
Good
```csharp
using Coven.Chat;
using Coven.Chat.Adapter;
using ConsoleAdapter = Coven.Chat.Adapter.Console.ConsoleAdapter;

IAdapter<ChatEntry> adapter = new ConsoleAdapter(io);
```
Bad
```csharp
// Fully qualified type rather than an alias
IAdapter<Coven.Chat.ChatEntry> adapter =
    new Coven.Chat.Adapter.Console.ConsoleAdapter(io);
```
