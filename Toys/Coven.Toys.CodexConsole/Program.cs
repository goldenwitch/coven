// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;
using Coven.Chat.Adapter.Console.Di;
using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Grimoire;
using Coven.Spellcasting.Agents.Codex.Di;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Validation;

namespace Coven.Toys.CodexConsole;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.ConfigureServices(services =>
        {
            // Console adapter stack
            services.AddConsoleChatAdapter(o => { o.InputSender = "console"; });

            // Books
            services.AddSingleton(new GuidebookBuilder()
                .AddSection("Guide", new[] { "Keep it concise and safe." })
                .Build());
            services.AddSingleton(sp =>
            {
                var scrivener = sp.GetRequiredService<IScrivener<ChatEntry>>();
                var sb = new SpellbookBuilder()
                    .AddSpell<string, string>(new Ask(scrivener));
                return sb.Build();
            });
            services.AddSingleton(new Testbook());

            // Wire Codex CLI agent (expects 'codex' on PATH). Workspace = current dir.
            services.AddCodexCliAgent<ChatEntry, DefaultChatEntryTranslator>(o =>
            {
                o.ExecutablePath = "codex";
                o.WorkspaceDirectory = Directory.GetCurrentDirectory();
                // Require a fixed shim path; no auto-discovery in the toy.
                o.ShimExecutablePath = Path.Combine(AppContext.BaseDirectory, "Coven.Spellcasting.Agents.Codex.McpShim.exe");
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
