// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coven.Sophia;
using Coven.Durables;

namespace Coven.Toys.MockProcess;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);
        builder.ConfigureLogging(lb => lb.ClearProviders());

        // Resolve optional Sophia file storage path from args/env
        var logPath = ResolveLogPath(args);

        builder.ConfigureServices(services =>
        {
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                services.AddSingleton<IDurableList<string>>(_ => new SimpleFileStorage<string>(logPath));
            }
            services.AddSophiaLogging(new SophiaLoggerOptions
            {
                Label = "toy",
                IncludeScopes = true,
                MinimumLevel = LogLevel.Information
            });

            services.AddHostedService<MockProcessOrchestrator>();
        });

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }

    private static string? ResolveLogPath(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--log" || args[i] == "-l") && i + 1 < args.Length)
            {
                var p = args[i + 1];
                if (!string.IsNullOrWhiteSpace(p)) return Path.GetFullPath(p);
            }
        }
        var env = Environment.GetEnvironmentVariable("MOCK_LOG_PATH");
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
        return null; // default to ConsoleList if not provided
    }
}
