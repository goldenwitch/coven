// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Gemini;
using Coven.Agents.OpenAI;
using Coven.Chat;
using Coven.Chat.Discord;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Streaming;
using Coven.Scriveners.FileScrivener;
using Coven.Transmutation;
using DiscordAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using OpenAiAgentMaxLengthWindowPolicy = Coven.Agents.OpenAI.AgentMaxLengthWindowPolicy;
using OpenAiAgentParagraphWindowPolicy = Coven.Agents.OpenAI.AgentParagraphWindowPolicy;

// Configuration (env-first with fallback to defaults below)
// Defaults: edit these to hardcode values when env vars are not present
string defaultDiscordToken = ""; // set your Discord bot token
ulong defaultDiscordChannelId = 0; // set your channel id
string defaultOpenAiApiKey = ""; // set your OpenAI API key
string defaultOpenAiModel = "gpt-5-2025-08-07"; // choose the model
string defaultGeminiApiKey = ""; // set your Gemini API key
string defaultGeminiModel = ""; // choose the model

// Environment overrides (optional)
string? envDiscordToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
string? envDiscordChannelId = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
string? envOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
string? envOpenAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
string? envGeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
string? envGeminiModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
bool useGemini = string.Equals(Environment.GetEnvironmentVariable("USE_GEMINI"), "true", StringComparison.OrdinalIgnoreCase);

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

GeminiClientConfig geminiConfig = new()
{
    ApiKey = string.IsNullOrWhiteSpace(envGeminiApiKey) ? defaultGeminiApiKey : envGeminiApiKey,
    Model = string.IsNullOrWhiteSpace(envGeminiModel) ? defaultGeminiModel : envGeminiModel
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
    FilePath = useGemini ? "./data/gemini-agent.ndjson" : "./data/openai-agent.ndjson"
});
builder.Services.AddDiscordChat(discordConfig);
if (useGemini)
{
    builder.Services.AddGeminiAgents(geminiConfig, registration => registration.EnableStreaming());
}
else
{
    builder.Services.AddOpenAIAgents(openAiConfig, registration => registration.EnableStreaming());
}

// Override windowing policies independently for outputs and thoughts
// Output chunk policy: paragraph-first with a tighter max length cap
builder.Services.AddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
    new CompositeWindowPolicy<AgentAfferentChunk>(
        new OpenAiAgentParagraphWindowPolicy(),
        new OpenAiAgentMaxLengthWindowPolicy(1024)));

// // Thought chunk policy: summary-marker, sentence, paragraph; independent cap
// builder.Services.AddScoped<IWindowPolicy<AgentAfferentThoughtChunk>>(_ =>
//     new CompositeWindowPolicy<AgentAfferentThoughtChunk>(
//         new AgentThoughtSummaryMarkerWindowPolicy(),
//         new AgentThoughtMaxLengthWindowPolicy(2048)));

// Override default OpenAI entry -> ResponseItem mapping with sample templating
builder.Services.AddScoped<ITransmuter<OpenAIEntry, ResponseItem?>, DiscordOpenAITemplatingTransmuter>();
builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, RouterBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
