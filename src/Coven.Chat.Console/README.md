# Coven.Chat.Console

Console chat adapter (leaf). Bridges standard input/output to `Coven.Chat` entries and runs a console daemon.

## What’s Inside

- Config: `ConsoleClientConfig` (`InputSender`, `OutputSender`).
- Gateway + Session: connect console I/O to chat journal.
- Transmuter: `ConsoleTransmuter` (ConsoleEntry ↔ ChatEntry).
- Journals: `IScrivener<ConsoleEntry>`, `IScrivener<ChatEntry>`.
- Daemon: `ConsoleChatDaemon`.

## Usage

```csharp
using Coven.Chat.Console;

services.AddConsoleChat(new ConsoleClientConfig
{
    InputSender = "console",
    OutputSender = "BOT"
});
```

This registers the console gateway/session, a chat journal (if none exists), a console scrivener, and the console daemon. Pair with `Coven.Chat` windowing if you want streamed drafts to become finalized chat messages.

## See Also

- Branch: `Coven.Chat` for windowing.
- Sample: See root README for swapping Discord→Console in Sample 01.
