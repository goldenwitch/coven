// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Coven.Core;
using Coven.Core.Di;
using Coven.Chat.Adapter.Console.Di;
using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Sophia;
using Coven.Durables;
using Coven.Spellcasting.Grimoire;

namespace Coven.Toys.ConsoleAgentChat;

// Entry point: wires Console chat adapter, registers the toy agent and books,
// and composes a single MagikUser pipeline that just starts the agent.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        IHostBuilder builder = Host.CreateDefaultBuilder(args);
        builder.ConfigureLogging(lb => lb.ClearProviders());

        builder.ConfigureServices(services =>
        {
            // Sophia logging: durable storage + provider
            // SophiaLogging will default to ConsoleList if no IDurableList<string> is registered
            services.AddSophiaLogging(new SophiaLoggerOptions
            {
                Label = "toy",
                IncludeScopes = true,
                MinimumLevel = LogLevel.Information
            });

            // Console adapter stack via convenience method
            services.AddConsoleChatAdapter(o =>
            {
                o.InputSender = "console";
            });

            // Books for the MagikUser (guide carries 8-ball responses)
            Guidebook guidebook = new GuidebookBuilder()
                .AddSections(new Dictionary<string, string>
                {
                    ["1"] = "It is certain.",
                    ["2"] = "Without a doubt.",
                    ["3"] = "You may rely on it.",
                    ["4"] = "Yes, definitely.",
                    ["5"] = "As I see it, yes.",
                    ["6"] = "Most likely.",
                    ["7"] = "Outlook good.",
                    ["8"] = "Signs point to yes.",
                    ["9"] = "Reply hazy, try again.",
                    ["10"] = "Ask again later.",
                    ["11"] = "Better not tell you now.",
                    ["12"] = "Cannot predict now.",
                    ["13"] = "Don’t count on it.",
                    ["14"] = "My reply is no.",
                    ["15"] = "Outlook not so good.",
                    ["16"] = "Very doubtful."
                })
                .Build();
            services.AddSingleton(guidebook);
            var spellbook = new SpellbookBuilder()
                .AddSpell(new CancelAgent())
                .Build();
            services.AddSingleton(spellbook);
            services.AddSingleton(new Testbook());

            // Agent + User (register agent with ambient control mapping)
            services.AddCovenAgent<ChatEntry, ConsoleToyAgent>();
            services.AddHostedService<ChatOrchestrator>();

            // Build a simple Coven with a single MagikUser that runs our agent
            services.BuildCoven(c => { c.AddBlock<Empty, Empty, AgentUser>(); c.Done(); });
        });

        using IHost host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}