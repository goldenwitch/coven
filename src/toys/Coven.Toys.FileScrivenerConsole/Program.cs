// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Scriveners.FileScrivener;
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

// ───────────────────────────────────────────────────────────────────────────
// DECLARATIVE COVENANT CONFIGURATION
// 
// This replaces the imperative PersistingEchoBlock pattern with a declarative covenant.
// Routes are defined at DI time—incoming messages echo back as outgoing.
// FileScrivener persists the journal to disk.
// ───────────────────────────────────────────────────────────────────────────

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

// Register FileScrivener<ChatEntry> before UseConsoleChat so TryAdd doesn't override
builder.Services.AddFileScrivener<ChatEntry>(fileConfig);

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseConsoleChat(console);

    coven.Covenant()
        .Connect(chat)
        .Routes(c =>
        {
            // Echo: incoming messages become outgoing with "Echo: " prefix
            // FileScrivener persists both to disk
            c.Route<ChatAfferent, ChatEfferent>(
                (msg, ct) => Task.FromResult(
                    new ChatEfferent("BOT", "Echo: " + msg.Text)));
        });
});

IHost host = builder.Build();

// Execute ritual - daemons auto-start via CovenExecutionScope
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
