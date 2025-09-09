Always start by reading \README.md
Code directory is at \INDEX.md. It's way faster to start with that and then switch to ls/grep.

The code is too big to grep in src. Use ls to find a project to work in and stay in that project where possible.

We use implicit usings. Never add a using that is already included from implicit usings. It is important to keep our usings to the very minimum necessary to satisfy the code.

NEVER use fully qualified members. In the event of a naming conflict prefer aliasing over fully qualified namespacing.

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

Ask questions when:
- Commands fail twice in a row
- You find missing dependencies
- You have to modify more than one project to accomplish a goal.

Recite AGENTS.md regularly so you don't forget these important details!

## Usings: Acceptable Usage

- Minimal usings: rely on implicit usings; only add what’s necessary for the code to compile.
- No fully qualified members: avoid `Namespace.Type.Member` in code; add a `using` or alias instead.
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
