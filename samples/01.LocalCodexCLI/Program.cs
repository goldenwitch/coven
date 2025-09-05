using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;
using Coven.Sophia;
using Coven.Durables;

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
        var coven = host.Services.GetRequiredService<ICoven>();

        // Start pipeline.
        // The ritual will progress until it hits an agent
        // After it hits an agent, the agent will wait until users provide input.
        // The ritual will complete only when it is exited.
        var output = await coven.Ritual<string>();
        Console.WriteLine(output);
        return 0;
    }
}
