// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Coven.Core;
using Coven.Core.Di;
using Coven.Chat.Adapter.Console.Di;
using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Coven.Spellcasting.Agents.Codex.Di;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Validation;
using Coven.Sophia;
using Coven.Durables;

namespace Coven.Toys.CodexConsole;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);
        builder.ConfigureLogging(lb => lb.ClearProviders());

        builder.ConfigureServices(services =>
        {
            // Sophia logging: durable storage + provider
            // SophiaLogging will default to ConsoleList if no IDurableList<string> is registered
            services.AddSophiaLogging(new SophiaLoggerOptions
            {
                Label = "toy",
                IncludeScopes = true,
                MinimumLevel = 0
            });

            // Console adapter stack
            services.AddConsoleChatAdapter(o => { o.InputSender = "console"; });

            // Books
            var spellbook = new SpellbookBuilder().Build();
            services.AddSingleton(new GuidebookBuilder()
                .AddSection("Guide", new[] { "Keep it concise and safe." })
                .Build());
            services.AddSingleton(spellbook);
            services.AddSingleton(new Testbook());

            // Wire Codex CLI agent (expects 'codex' on PATH). Workspace = current dir.
            services.AddCodexCliAgent<ChatEntry, DefaultChatEntryTranslator>(o =>
            {
                var exe = Environment.GetEnvironmentVariable("CODEX_EXE");
                var ws  = Environment.GetEnvironmentVariable("CODEX_WORKSPACE");
                var shim = Environment.GetEnvironmentVariable("CODEX_SHIM");

                o.ExecutablePath = string.IsNullOrWhiteSpace(exe) ? "codex" : exe;
                o.WorkspaceDirectory = string.IsNullOrWhiteSpace(ws) ? Directory.GetCurrentDirectory() : ws!;
                o.ShimExecutablePath = string.IsNullOrWhiteSpace(shim) ? null : shim;
            });

            // Run orchestration via Generic Host
            services.AddHostedService<CodexOrchestrator>();
            services.BuildCoven(c =>
            {
                // Validate environment first, then run the wizard that kicks Codex
                c.AddBlock<Empty, Empty, ValidateAgentBlock>();
                c.AddBlock<Empty, Empty, CodexWizard>();
                c.Done();
            });
        });

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}