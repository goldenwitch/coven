using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;
using Coven.Sophia;
using Coven.Durables;
using Coven.Chat;
using Coven.Chat.Adapter;
using Coven.Chat.Adapter.Console.Di;

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
            // Run orchestration via Generic Host
            services.AddHostedService<SampleOrchestrator>();
            services.BuildCoven(c =>
            {
                c.AddThurible<string, int>(
                    label: "Sanitize string",
                    func: async (input, logs) =>
                    {
                        await logs.Append($"during-smoke-log : {input}");
                        return input.Length;
                    },
                    storageFactory: sp => sp.GetRequiredService<IDurableList<string>>()
                );
                c.Done();
            });
        });

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
