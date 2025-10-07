using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Toys.DiscordChat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configuration
DiscordClientConfig discordClientConfig = new()
{
    BotToken = "",
    ChannelId = 1330113333424164865 // If you are leveraging a specific channel put the id here.
};

// Register all of our DI stuff
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDiscordChat(discordClientConfig);
builder.Services.BuildCoven(CovenServiceBuilder => CovenServiceBuilder.MagikBlock<Empty, Empty, EchoBlock>().Done());

IHost host = builder.Build();

// Execute our ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
