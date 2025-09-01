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

        // Start pipeline from string (goal)
        var output = await coven.Ritual<string>();
        Console.WriteLine(output);
        return 0;
    }
}
