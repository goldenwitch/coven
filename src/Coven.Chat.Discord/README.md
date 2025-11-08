# Coven.Chat.Discord

Discord chat adapter (leaf). Bridges a Discord channel to `Coven.Chat` entries with a daemon and default windowing suitable for Discord limits.

## What’s Inside

- Config: `DiscordClientConfig` (bot token, `ChannelId`).
- Gateway + Session: `DiscordGatewayConnection`, `DiscordChatSessionFactory`.
- Transmuter: `DiscordTransmuter` (DiscordEntry ↔ ChatEntry).
- Journals: `IScrivener<DiscordEntry>`, `IScrivener<ChatEntry>`.
- Daemon: `DiscordChatDaemon`.
- Windowing defaults: paragraph OR 2000‑char cap; shatter drafts accordingly.

## Prerequisites

- Discord bot token with Message Content intent enabled.
- Bot invited to the server and has read/write permissions for the target channel.
- Channel ID (copy via Discord Developer Mode).

## Usage

```csharp
using Coven.Chat.Discord;

services.AddDiscordChat(new DiscordClientConfig
{
    BotToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")!,
    ChannelId = ulong.Parse(Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID")!)
});
```

This registers the Discord client, session factory, journals, transmuter, daemon, and windowing/shattering tuned for Discord.

## See Also

- Branch: `Coven.Chat`.
- Sample: `src/samples/01.DiscordAgent`.
