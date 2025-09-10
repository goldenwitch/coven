using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;
using Coven.Sophia;
using Coven.Durables;
using Coven.Chat;
using Coven.Chat.Adapter;
using Coven.Chat.Adapter.Console.Di;
using System.Collections.ObjectModel;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Coven.Spellcasting.Agents.Codex.Di;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting;

namespace Coven.Samples.LocalCodexCLI;


internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "coven-thurible-tests");
        Directory.CreateDirectory(tempDir);
        var _path = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".json");

        var builder = Host.CreateDefaultBuilder();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IDurableList<string>>(_ => new SimpleFileStorage<string>(_path));

            // Console chat adapter: wires IConsoleIO, IAdapter<ChatEntry>, IAdapterHost<ChatEntry>, and a default scrivener
            services.AddConsoleChatAdapter(o => { o.InputSender = "console"; });

            // Spellbook + books for Wizard
            var spellbook = new SpellbookBuilder().Build();
            services.AddSingleton(new GuidebookBuilder()
                .AddSection("Guide", new[] { "Wizard guide: keep it concise." })
                .Build());
            services.AddSingleton(spellbook);
            services.AddSingleton(new Testbook());

            // Wire Codex CLI agent (expects 'codex' on PATH). Workspace = current dir.
            services.AddCodexCliAgent<ChatEntry, DefaultChatEntryTranslator>(o =>
            {
                o.ExecutablePath = "codex"; // or absolute path
                o.WorkspaceDirectory = Directory.GetCurrentDirectory();
                o.ShimExecutablePath = null;
                // Spells will be resolved from DI Spellbook if present.
            });

            // Run orchestration via Generic Host
            services.AddHostedService<SampleOrchestrator>();
            services.BuildCoven(c =>
            {
                // Entry block for Ritual<string>() that starts our agent
                c.AddBlock<Empty, Empty, Wizard>();
                c.Done();
            });
        });

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
