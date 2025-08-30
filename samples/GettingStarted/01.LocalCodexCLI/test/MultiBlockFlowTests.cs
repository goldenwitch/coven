using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Coven.Core;
using Coven.Core.Di;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Validation;

namespace Coven.Samples.LocalCodexCLI.Tests;

public class MultiBlockFlowTests
{
    private sealed class FakeAgent : ICovenAgent<FixSpell, string>
    {
        public string Id => "fake";
        public Task<string> CastSpellAsync(FixSpell input, SpellContext? context = null, CancellationToken ct = default)
        {
            var mode = context?.Permissions?.Allows<WriteFile>() == true ? "edit" : "suggest";
            var cwd = context?.ContextUri?.IsAbsoluteUri == true ? context.ContextUri.LocalPath : string.Empty;
            var text = $"{input.Goal}|{input.SpellVersion}|{input.TestSuite}|{mode}|{cwd}";
            return Task.FromResult(text);
        }
    }

    private sealed class FakeValidation : IAgentValidation
    {
        public string AgentId => "fake";
        public int Calls { get; private set; }
        public Task<AgentValidationResult> ValidateAsync(SpellContext? context = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(AgentValidationResult.Noop());
        }
    }

    [Fact]
    public async Task Translates_String_To_ChangeRequest_Using_Config()
    {
        var repo = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(repo);

        var services = new ServiceCollection();
        services.AddSingleton(new SampleConfig { RepoRoot = repo });

        services.BuildCoven(c =>
        {
            c.AddBlock<string, ChangeRequest, MakeChangeRequestBlock>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var cr = await coven.Ritual<string, ChangeRequest>("goal-text");
        Assert.Equal(Path.GetFullPath(repo).TrimEnd(Path.DirectorySeparatorChar),
                     Path.GetFullPath(cr.RepoRoot).TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal("goal-text", cr.Goal);
    }

    [Fact]
    public async Task Full_Pipeline_Validates_And_Invokes_Agent()
    {
        var repo = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(repo);

        var services = new ServiceCollection();
        services.AddSingleton(new SampleConfig { RepoRoot = repo });
        var validation = new FakeValidation();
        services.AddSingleton<IAgentValidation>(validation);
        services.AddSingleton<ICovenAgent<FixSpell, string>, FakeAgent>();

        services.BuildCoven(c =>
        {
            c.AddBlock<string, ChangeRequest, MakeChangeRequestBlock>();
            c.AddBlock<ChangeRequest, SpellContext, MakeContextBlock>();
            c.AddBlock<SpellContext, SpellContext, ValidateAgentBlock>();
            c.AddBlock<SpellContext, string>(sp =>
                new SpellUserFromContext(
                    sp.GetRequiredService<ICovenAgent<FixSpell, string>>(),
                    goal: "do-the-thing"));
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var output = await coven.Ritual<string, string>("ignored-goal-because-configured-in-user");

        Assert.True(validation.Calls >= 1);
        Assert.Contains("do-the-thing", output);
        Assert.Contains("0.1", output);    // default spell version
        Assert.Contains("smoke", output);  // default test suite
        Assert.Contains("edit", output);   // permission reflected
        Assert.Contains(Path.GetFullPath(repo).TrimEnd(Path.DirectorySeparatorChar), output);
    }
}

