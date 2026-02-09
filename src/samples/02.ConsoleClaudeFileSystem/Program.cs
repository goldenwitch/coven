// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Agents.FileSystem;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Coven.FileSystem.Posix;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ───────────────────────────────────────────────────────────────────────────
// Sample 02: Console Chat + Claude Agent + FileSystem Tool Calls
//
// Demonstrates the covenant pattern:
// - Console chat for user interaction
// - Claude agent for LLM reasoning
// - FileSystem branch as a tool (read files via agent tool calls)
// - Companion library bridging agent tool calls to file system operations
// ───────────────────────────────────────────────────────────────────────────

// Configuration
ConsoleClientConfig consoleConfig = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

ClaudeClientConfig claudeConfig = new()
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "",
    Model = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-sonnet-4-20250514",
    SystemPrompt = "You are a helpful assistant with access to the file system. You can read files when asked. Use the read_file tool to read file contents."
};

// File system root — where the agent can read files
string fsRoot = Environment.GetEnvironmentVariable("FS_ROOT") ?? Directory.GetCurrentDirectory();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

// Register companion library (tool definitions + transmuters)
builder.Services.AddFileSystemCompanion();

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseConsoleChat(consoleConfig);
    BranchManifest agents = coven.UseClaudeAgents(claudeConfig, reg => reg.EnableTools());
    BranchManifest filesystem = coven.UsePosixFileSystem(fsRoot);

    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Connect(filesystem)
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

            // Agents ↔ FileSystem (via companion transmuters)
            c.RouteFileSystemTools();

            // Thoughts are terminal (not displayed)
            c.Terminal<AgentThought>();
        });
});

IHost host = builder.Build();

// Execute ritual — daemons auto-start via CovenExecutionScope
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
