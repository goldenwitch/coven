// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.OpenAI;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.Core.Streaming;
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

// ───────────────────────────────────────────────────────────────────────────
// DECLARATIVE COVENANT CONFIGURATION
// 
// This replaces the imperative RouterBlock pattern with a declarative covenant.
// No RouterBlock class needed—routes are defined at DI time and validated.
// ───────────────────────────────────────────────────────────────────────────

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

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

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseConsoleChat(consoleConfig);
    BranchManifest agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

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

            // Thoughts displayed to console (original behavior)
            c.Route<AgentThought, ChatEfferent>(
                (t, ct) => Task.FromResult(
                    new ChatEfferent("BOT", t.Text)));

            // Streaming: chunks are terminal (console doesn't support chunk display)
            c.Terminal<AgentAfferentChunk>();
            c.Terminal<AgentAfferentThoughtChunk>();
        });
});

IHost host = builder.Build();

// Execute ritual - daemons auto-start via CovenExecutionScope
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
