using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Chat.Adapter.Console.Di;

namespace Coven.Toys.ConsoleEcho;

// Entry point for the ConsoleEcho toy:
// wires the console chat adapter and starts an orchestrator that echoes input.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        IHostBuilder builder = Host.CreateDefaultBuilder(args);

        builder.ConfigureServices(services =>
        {
            // Console adapter stack via convenience method
            services.AddConsoleChatAdapter(o =>
            {
                o.InputSender = "console";
            });

            // Orchestrator that pumps the adapter and echoes back
            services.AddHostedService<EchoOrchestrator>();
        });

        using IHost host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
