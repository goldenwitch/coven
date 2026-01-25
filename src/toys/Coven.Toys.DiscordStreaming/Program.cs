// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Chat.Discord;
using Coven.Chat.Shattering;
using Coven.Chat.Windowing;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
DiscordClientConfig discordClientConfig = new()
{
    BotToken = "", // set your Discord bot token
    ChannelId = 0   // set your channel id
};

// ───────────────────────────────────────────────────────────────────────────
// DECLARATIVE COVENANT CONFIGURATION
// 
// This replaces the imperative StreamingBlock pattern with a declarative covenant.
// Routes are defined at DI time—incoming messages become draft outgoing.
// ───────────────────────────────────────────────────────────────────────────

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

// Toy overrides: Sentence then 2k shatter; sentence+2k windowing
builder.Services.AddScoped<IShatterPolicy<ChatEntry>>(sp =>
    new ChainedShatterPolicy<ChatEntry>(
        new ChatSentenceShatterPolicy(),
        new ChatChunkMaxLengthShatterPolicy(2000)
    ));
builder.Services.AddScoped<IWindowPolicy<ChatChunk>>(_ =>
    new CompositeWindowPolicy<ChatChunk>(
        new ChatSentenceWindowPolicy(),
        new ChatMaxLengthWindowPolicy(2000))
    );

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseDiscordChat(discordClientConfig);

    coven.Covenant()
        .Connect(chat)
        .Routes(c =>
        {
            // Streaming echo: incoming messages become draft outgoing (triggers shattering)
            c.Route<ChatAfferent, ChatEfferentDraft>(
                (msg, ct) => Task.FromResult(
                    new ChatEfferentDraft("BOT", msg.Text)));
        });
});

IHost host = builder.Build();

// Execute ritual - daemons auto-start via CovenExecutionScope
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
