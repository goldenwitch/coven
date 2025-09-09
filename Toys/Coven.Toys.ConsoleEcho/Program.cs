using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Chat.Adapter.Console.Di;

namespace Coven.Toys.ConsoleEcho;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.ConfigureServices(services =>
        {
            // Console adapter stack via convenience method
            services.AddConsoleChatAdapter(o => { o.InputSender = "console"; });

            // Orchestrator that pumps the adapter and echoes back
            services.AddHostedService<EchoOrchestrator>();
        });

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
