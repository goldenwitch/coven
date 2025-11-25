// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Scriveners.FileScrivener;
using Coven.Toys.FileScrivenerConsole;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration (toy-friendly defaults)
ConsoleClientConfig console = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

FileScrivenerConfig fileConfig = new()
{
    FilePath = "./data/console-chat.ndjson",
    FlushThreshold = 1,
    FlushQueueCapacity = 8
};

// Register DI
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

// Register FileScrivener<ChatEntry> before AddConsoleChat so TryAdd doesn't override
builder.Services.AddFileScrivener<ChatEntry>(fileConfig);
builder.Services.AddConsoleChat(console);

// Wire a simple echo block that starts daemons and echoes input
builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, PersistingEchoBlock>().Done());

IHost host = builder.Build();

// Execute ritual
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
