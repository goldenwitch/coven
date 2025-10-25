using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Toys.DiscordStreaming;
using Coven.Chat.Shattering;
using Coven.Chat.Windowing;
using Coven.Core.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Chat;

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

// Toy overrides: use sentence shattering and a composite window policy
builder.Services.AddScoped<IShatterPolicy<ChatEntry>, ChatSentenceShatterPolicy>();
builder.Services.AddScoped<IWindowPolicy<ChatChunk>>(_ =>
    new CompositeWindowPolicy<ChatChunk>(
        new ChatSentenceWindowPolicy())
    );

builder.Services.BuildCoven(b => b.MagikBlock<Empty, Empty, StreamingBlock>().Done());

IHost host = builder.Build();

// Execute our ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
