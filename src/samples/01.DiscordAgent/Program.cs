// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.OpenAI;
using Coven.Chat;
using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using DiscordAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Transmutation;
using OpenAI.Responses;
using Coven.Core.Streaming;
using Coven.Agents;
using Coven.Scriveners.FileScrivener;

// Configuration (env-first with fallback to defaults below)
// Defaults: edit these to hardcode values when env vars are not present
string defaultDiscordToken = ""; // set your Discord bot token
ulong defaultDiscordChannelId = 0; // set your channel id
string defaultOpenAiApiKey = ""; // set your OpenAI API key
string defaultOpenAiModel = "gpt-5-2025-08-07"; // choose the model

// Environment overrides (optional)
string? envDiscordToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
string? envDiscordChannelId = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
string? envOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
string? envOpenAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");

ulong channelId = defaultDiscordChannelId;
if (!string.IsNullOrWhiteSpace(envDiscordChannelId) && ulong.TryParse(envDiscordChannelId, out ulong parsed))
{
    channelId = parsed;
}

DiscordClientConfig discordConfig = new()
{
    BotToken = string.IsNullOrWhiteSpace(envDiscordToken) ? defaultDiscordToken : envDiscordToken,
    ChannelId = channelId
};

OpenAIClientConfig openAiConfig = new()
{
    ApiKey = string.IsNullOrWhiteSpace(envOpenAiApiKey) ? defaultOpenAiApiKey : envOpenAiApiKey,
    Model = string.IsNullOrWhiteSpace(envOpenAiModel) ? defaultOpenAiModel : envOpenAiModel
};

// Register DI
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
// Persist journals to disk using FileScrivener (registered before branches to avoid TryAdd overrides)
builder.Services.AddFileScrivener<ChatEntry>(new FileScrivenerConfig
{
    FilePath = "./data/discord-chat.ndjson",
    FlushThreshold = 1
});
builder.Services.AddFileScrivener<AgentEntry>(new FileScrivenerConfig
{
    FilePath = "./data/openai-agent.ndjson"
});
builder.Services.AddDiscordChat(discordConfig);
builder.Services.AddOpenAIAgents(openAiConfig, registration =>
{
    registration.EnableStreaming();
});

// Override windowing policies independently for outputs and thoughts
// Output chunk policy: paragraph-first with a tighter max length cap
builder.Services.AddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
    new CompositeWindowPolicy<AgentAfferentChunk>(
        new AgentParagraphWindowPolicy(),
        new AgentMaxLengthWindowPolicy(1024)));

// Override default OpenAI entry → ResponseItem mapping with sample templating
builder.Services.AddScoped<ITransmuter<OpenAIEntry, ResponseItem?>, DiscordOpenAITemplatingTransmuter>();
builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, RouterBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
