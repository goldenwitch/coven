using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coven.Core;
using Coven.Core.Di;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Coven.Spellcasting.Agents.Validation;

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
        // Arguments: --repo <path> --goal <text> [--codex <exe>]
        if (args.Length == 0 || HasArg(args, "-h") || HasArg(args, "--help"))
        {
            PrintHelp();
            return 2;
        }

        var repo = GetValue(args, "--repo") ?? Environment.CurrentDirectory;
        var goal = GetValue(args, "--goal");
        var codexPath = GetValue(args, "--codex");

        if (string.IsNullOrWhiteSpace(goal))
        {
            Console.Error.WriteLine("error: --goal is required.\n");
            PrintHelp();
            return 2;
        }

        if (!Directory.Exists(repo))
        {
            Console.Error.WriteLine($"error: repo directory not found: {repo}");
            return 2;
        }

        // Use HostApplicationBuilder for DI + configuration
        var builder = Host.CreateApplicationBuilder(args);

        // DI wiring: register Codex CLI agent and compose pipeline
        builder.Services.AddSingleton(new SampleConfig { RepoRoot = repo });
        builder.Services.AddSingleton<ICovenAgent<FixSpell, string>>(sp =>
        {
            string ToPrompt(FixSpell f) => $"goal={f.Goal}; version={f.SpellVersion}; suite={f.TestSuite}";
            string Parse(string s) => s.Trim();
            var opts = new CodexCliAgent<FixSpell, string>.Options
            {
                ExecutablePath = string.IsNullOrWhiteSpace(codexPath) ? "codex" : codexPath
            };
            return new CodexCliAgent<FixSpell, string>(ToPrompt, Parse, opts);
        });
        builder.Services.AddSingleton<IAgentValidation>(sp => new CodexCliValidation(new CodexCliValidation.Options
        {
            ExecutablePath = string.IsNullOrWhiteSpace(codexPath) ? "codex" : codexPath
        }));

        builder.BuildCoven(c =>
        {
            // string (goal) -> ChangeRequest
            c.AddBlock<string, ChangeRequest, MakeChangeRequestBlock>();
            // ChangeRequest -> SpellContext
            c.AddBlock<ChangeRequest, SpellContext, MakeContextBlock>();
            // SpellContext -> SpellContext (validation)
            c.AddBlock<SpellContext, SpellContext, ValidateAgentBlock>();
            // SpellContext -> string (invoke agent)
            c.AddBlock<SpellContext, string>(sp =>
                new SpellUserFromContext(
                    sp.GetRequiredService<ICovenAgent<FixSpell, string>>(),
                    goal!));
            c.Done();
        });

        using var host = builder.Build();
        var coven = host.Services.GetRequiredService<ICoven>();

        // Start pipeline from string (goal)
        var output = await coven.Ritual<string, string>(goal!);
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
