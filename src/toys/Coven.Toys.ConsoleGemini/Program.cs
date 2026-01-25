// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.Gemini;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Toys.ConsoleGemini;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
ConsoleClientConfig consoleConfig = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

GeminiClientConfig geminiConfig = new()
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "", // set your key or use env var
    Model = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-2.0-flash"
};

// Register DI
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddConsoleChat(consoleConfig);
builder.Services.AddGeminiAgents(geminiConfig);
builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, RouterBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
