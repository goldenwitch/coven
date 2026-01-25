// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
ConsoleClientConfig config = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

// ───────────────────────────────────────────────────────────────────────────
// DECLARATIVE COVENANT CONFIGURATION
// 
// This replaces the imperative EchoBlock pattern with a declarative covenant.
// Routes are defined at DI time—incoming messages echo back as outgoing.
// ───────────────────────────────────────────────────────────────────────────

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseConsoleChat(config);

    coven.Covenant()
        .Connect(chat)
        .Routes(c =>
        {
            // Echo: incoming messages become outgoing with "Echo: " prefix
            c.Route<ChatAfferent, ChatEfferent>(
                (msg, ct) => Task.FromResult(
                    new ChatEfferent("BOT", "Echo: " + msg.Text)));
        });
});

IHost host = builder.Build();

// Execute ritual - daemons auto-start via CovenExecutionScope
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());

