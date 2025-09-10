using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Coven.Chat.Adapter.Console.Di;
using Coven.Sophia;
using Coven.Durables;

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
            // Sophia logging: durable storage + provider
            // SophiaLogging will default to ConsoleList if no IDurableList<string> is registered
            services.AddSophiaLogging(new SophiaLoggerOptions
            {
                Label = "toy",
                IncludeScopes = true,
                MinimumLevel = LogLevel.Information,
                CompactBreadcrumbs = true
            });

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
