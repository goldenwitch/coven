// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.OpenAI;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Streaming;
using Coven.Toys.ConsoleOpenAIStreaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
ConsoleClientConfig consoleConfig = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

OpenAIClientConfig openAiConfig = new()
{
    ApiKey = "", // set your key
    Model = "gpt-5-2025-08-07"
};

// Register DI
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddConsoleChat(consoleConfig);

// Enable OpenAI streaming
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

// Thought chunk policy: summary-marker, sentence, paragraph; independent cap
builder.Services.AddScoped<IWindowPolicy<AgentAfferentThoughtChunk>>(_ =>
    new CompositeWindowPolicy<AgentAfferentThoughtChunk>(
        new AgentThoughtSummaryMarkerWindowPolicy(),
        new AgentThoughtSentenceWindowPolicy(),
        new AgentThoughtMaxLengthWindowPolicy(2048)));

builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, RouterBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
