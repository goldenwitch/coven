using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;

namespace Coven.Samples.LocalCodexCLI;


internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Use HostApplicationBuilder for DI + configuration
        var builder = Host.CreateApplicationBuilder(args);

        // Here is where build our ritual
        builder.BuildCoven(c =>
        {
            c.Done();
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
