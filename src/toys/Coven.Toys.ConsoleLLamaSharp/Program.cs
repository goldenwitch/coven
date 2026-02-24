// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.LLamaSharp;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using LLama.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Tell LLamaSharp to load the CUDA backend (must happen before any model load)
NativeLibraryConfig.All.WithCuda();

// Configuration
ConsoleClientConfig consoleConfig = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

LLamaSharpClientConfig llamaConfig = new()
{
    ModelPath = @"D:\GPT-oss\gpt-oss-20b-UD-Q8_K_XL.gguf",
    GpuLayerCount = int.TryParse(Environment.GetEnvironmentVariable("LLAMASHARP_GPU_LAYERS"), out int layers) ? layers : 20,
    ContextSize = uint.TryParse(Environment.GetEnvironmentVariable("LLAMASHARP_CONTEXT_SIZE"), out uint ctx) ? ctx : 2048,
    SystemPrompt = "You are a helpful assistant."
};

// ───────────────────────────────────────────────────────────────────────────
// DECLARATIVE COVENANT CONFIGURATION
//
// This replaces the imperative RouterBlock pattern with a declarative covenant.
// No RouterBlock class needed—routes are defined at DI time and validated.
// ───────────────────────────────────────────────────────────────────────────

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseConsoleChat(consoleConfig);
    BranchManifest agents = coven.UseLLamaSharpAgents(llamaConfig);

    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            // Chat → Agents: incoming messages become prompts
            c.Route<ChatAfferent, AgentPrompt>(
                (msg, ct) => Task.FromResult(
                    new AgentPrompt(msg.Sender, msg.Text)));

            // Agents → Chat: responses become outgoing messages
            c.Route<AgentResponse, ChatEfferent>(
                (r, ct) => Task.FromResult(
                    new ChatEfferent("BOT", r.Text)));
        });
});

IHost host = builder.Build();

// Inhabit — start daemons and keep them alive until Ctrl+C
using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Inhabit(cts.Token);
