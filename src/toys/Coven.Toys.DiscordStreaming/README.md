# Coven.Toys.DiscordStreaming

Discord bot with streaming message output. Demonstrates shatter and windowing policies for Discord's message limits.

## Prerequisites

- Discord bot token
- Discord channel ID

Set these in `Program.cs` before running.

## How to Run

```bash
dotnet run
```

The bot streams responses with sentence-based shattering and a 2000-character limit (Discord's max message length).
