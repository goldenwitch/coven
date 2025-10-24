using Coven.Agents.OpenAI;
using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using DiscordAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Transmutation;
using OpenAI.Responses;

// Configuration
DiscordClientConfig discordConfig = new()
{
    BotToken = "", // set your Discord bot token
    ChannelId = 0   // set your channel id
};

OpenAIClientConfig openAiConfig = new()
{
    ApiKey = "",            // set your OpenAI API key
    Model = "gpt-5-2025-08-07" // choose the model
};

// Register DI
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddDiscordChat(discordConfig);
builder.Services.AddOpenAIAgents(openAiConfig);
// Override default OpenAI entry → ResponseItem mapping with sample templating
builder.Services.AddScoped<ITransmuter<OpenAIEntry, ResponseItem?>, DiscordOpenAITemplatingTransmuter>();
builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, RouterBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
