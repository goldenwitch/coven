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
    ChannelId = 0   // set your channel id
};

// Register all of our DI stuff
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddDiscordChat(discordClientConfig);

// Toy overrides: Sentence then 2k shatter; sentence+2k windowing
builder.Services.AddScoped<IShatterPolicy<ChatEntry>>(sp =>
    new ChainedShatterPolicy<ChatEntry>(
        new ChatSentenceShatterPolicy(),
        new ChatChunkMaxLengthShatterPolicy(2000)
    ));
builder.Services.AddScoped<IWindowPolicy<ChatChunk>>(_ =>
    new CompositeWindowPolicy<ChatChunk>(
        new ChatSentenceWindowPolicy(),
        new ChatMaxLengthWindowPolicy(2000))
    );

builder.Services.BuildCoven(b => b.MagikBlock<Empty, Empty, StreamingBlock>().Done());

IHost host = builder.Build();

// Execute our ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
