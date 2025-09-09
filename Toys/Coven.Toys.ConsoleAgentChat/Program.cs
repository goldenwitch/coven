using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;
using Coven.Chat.Adapter.Console.Di;
using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;

namespace Coven.Toys.ConsoleAgentChat;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.ConfigureServices(services =>
        {
            // Console adapter stack via convenience method
            services.AddConsoleChatAdapter(o => { o.InputSender = "console"; });

            // Books for the MagikUser (guide carries 8-ball responses)
            var responses = new[]
            {
                "It is certain.",
                "Without a doubt.",
                "You may rely on it.",
                "Yes, definitely.",
                "As I see it, yes.",
                "Most likely.",
                "Outlook good.",
                "Signs point to yes.",
                "Reply hazy, try again.",
                "Ask again later.",
                "Better not tell you now.",
                "Cannot predict now.",
                "Don’t count on it.",
                "My reply is no.",
                "Outlook not so good.",
                "Very doubtful."
            };
            var guidebook = new GuidebookBuilder()
                .AddSections(new Dictionary<string, string>(responses.Length)
                {
                    ["1"] = responses[0],
                    ["2"] = responses[1],
                    ["3"] = responses[2],
                    ["4"] = responses[3],
                    ["5"] = responses[4],
                    ["6"] = responses[5],
                    ["7"] = responses[6],
                    ["8"] = responses[7],
                    ["9"] = responses[8],
                    ["10"] = responses[9],
                    ["11"] = responses[10],
                    ["12"] = responses[11],
                    ["13"] = responses[12],
                    ["14"] = responses[13],
                    ["15"] = responses[14],
                    ["16"] = responses[15],
                })
                .Build();
            services.AddSingleton(guidebook);
            services.AddSingleton(new SpellbookBuilder().Build());
            services.AddSingleton(new Testbook());

            // Agent + User
            services.AddSingleton<ICovenAgent<ChatEntry>, ConsoleToyAgent>();
            services.AddHostedService<ChatOrchestrator>();

            // Build a simple Coven with a single MagikUser that runs our agent
            services.BuildCoven(c => { c.AddBlock<Empty, string, AgentUser>(); c.Done(); });
        });

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
