using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Toys.DiscordChat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
DiscordClientConfig discordClientConfig = new()
{
    BotToken = "",
    ChannelId = 123
};

// Register all of our DI stuff
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(builder => builder.AddConsole());
builder.Services.AddDiscordChat(discordClientConfig);
builder.Services.BuildCoven(CovenServiceBuilder => CovenServiceBuilder.MagikBlock<Empty, Empty, EchoBlock>().Done());

IHost host = builder.Build();

// Execute our ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
