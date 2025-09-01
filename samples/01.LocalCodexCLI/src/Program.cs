using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Coven.Spellcasting.Agents.Validation;
using System.Text.RegularExpressions;

namespace Coven.Samples.LocalCodexCLI;

// Simple runtime config for DI
internal sealed class SampleConfig
{
    public string RepoRoot { get; init; } = string.Empty;
}

// Inputs + payload
internal sealed record ChangeRequest(string RepoRoot, string Goal);
internal sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

// Block 0: Translate from string (goal) to ChangeRequest using DI config
internal sealed class MakeChangeRequestBlock : IMagikBlock<string, ChangeRequest>
{
    private readonly SampleConfig _cfg;
    public MakeChangeRequestBlock(SampleConfig cfg) { _cfg = cfg; }
    public Task<ChangeRequest> DoMagik(string goal)
    {
        var repo = string.IsNullOrWhiteSpace(_cfg.RepoRoot) ? Environment.CurrentDirectory : _cfg.RepoRoot;
        return Task.FromResult(new ChangeRequest(repo, goal));
    }
}

// Block 1: Build SpellContext from ChangeRequest
internal sealed class MakeContextBlock : IMagikBlock<ChangeRequest, SpellContext>
{
    public Task<SpellContext> DoMagik(ChangeRequest input)
    {
        var ctx = new SpellContext
        {
            ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
            Permissions = AgentPermissions.AutoEdit()
        };
        return Task.FromResult(ctx);
    }
}

// Block 3: User that invokes the agent given a SpellContext (after validation)
internal sealed class SpellUserFromContext : MagikUser<SpellContext, string>
{
    private readonly ICovenAgent<FixSpell, string> _agent;
    private readonly string _goal;
    public SpellUserFromContext(ICovenAgent<FixSpell, string> agent, string goal)
    { _agent = agent; _goal = goal; }

    protected override Task<string> InvokeAsync(
        SpellContext input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct)
    {
        var payload = new FixSpell(
            guide.Payload.Markdown,
            spell.Payload.Version,
            test.Payload.Suite,
            _goal);
        return _agent.CastSpellAsync(payload, input, ct);
    }
}

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Use HostApplicationBuilder for DI + configuration
        var builder = Host.CreateApplicationBuilder(args);



        builder.Services.AddSingleton<IAgentValidation>(sp => new CodexCliValidation(new CodexCliValidation.Options
        {
            ExecutablePath = string.IsNullOrWhiteSpace(codexPath) ? "codex" : codexPath
        }));

        builder.Services.AddSingleton<Wizard.PreferenceGuidebook>();
        builder.Services.AddSingleton<Wizard.ISharedStore, Wizard.InMemorySharedStore>();
        builder.Services.AddSingleton<Wizard.CodingSpellbook>();
        builder.Services.AddSingleton<Wizard.DeltaTestbook>();
        builder.Services.AddSingleton<Oracle.IWebSpellbook, Oracle.WebSpellbook>();
        builder.Services.AddSingleton<Oracle.SearchGuidebook>();

        // Here is where build our ritual
        builder.BuildCoven(c =>
        {
            

            c.Done();
        });

        using var host = builder.Build();
        var coven = host.Services.GetRequiredService<ICoven>();

        // Start pipeline from string (goal)
        var output = await coven.Ritual<string, string>();
        Console.WriteLine(output);
        return 0;
    }

    private static bool HasArg(string[] args, string key)
    {
        foreach (var a in args)
        {
            if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string? GetValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) return args[i + 1];
                return null;
            }
        }
        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: 01.LocalCodexCLI --repo <path> --goal <text> [--codex <exe>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --repo <path>   Repository root (defaults to current directory)");
        Console.WriteLine("  --goal <text>   High-level goal given to Codex CLI");
        Console.WriteLine("  --codex <exe>   Optional path to codex executable (defaults to 'codex')");
    }
}
