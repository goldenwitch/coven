using Coven.Agents.OpenAI;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Toys.ConsoleOpenAI;
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
builder.Services.AddOpenAIAgents(openAiConfig);
builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, RouterBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
