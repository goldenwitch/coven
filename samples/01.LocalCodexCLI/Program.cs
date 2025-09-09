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

            // Provide a simple scrivener for Codex output lines
            services.AddSingleton<IScrivener<string>, InMemoryScrivener<string>>();

            // Wire Codex CLI agent (expects 'codex' on PATH). Workspace = current dir.
            services.AddSingleton<ICovenAgent<string>>(sp =>
            {
                var scrivener = sp.GetRequiredService<IScrivener<string>>();
                var codexPath = "codex"; // or absolute path to executable
                var workspaceDir = Directory.GetCurrentDirectory();
                var sb = sp.GetRequiredService<Spellbook>();
                // Provide invocation mapping from the spellbook (spell instances)
                return new CodexCliAgent<string>(codexPath, workspaceDir, scrivener, null, sb.Spells);
            });
            
            // Run orchestration via Generic Host
            services.AddHostedService<SampleOrchestrator>();
            services.BuildCoven(c =>
            {
                // Entry block for Ritual<string>() that starts our agent
                c.AddBlock<Empty, string, Wizard>();
                c.Done();
            });
        });

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
