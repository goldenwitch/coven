using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Toys.ConsoleChat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
ConsoleClientConfig config = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

// Register DI
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddConsoleChat(config);
builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, EchoBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());

