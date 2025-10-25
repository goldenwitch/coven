using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Toys.DiscordStreaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
DiscordClientConfig discordClientConfig = new()
{
    BotToken = "", // set your Discord bot token
    ChannelId = 123   // set your channel id
};

// Register all of our DI stuff
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddDiscordChat(discordClientConfig);
builder.Services.BuildCoven(b => b.MagikBlock<Empty, Empty, StreamingBlock>().Done());

IHost host = builder.Build();

// Execute our ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
