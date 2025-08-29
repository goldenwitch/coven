using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Di;
using Coven.Spellcasting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public class E2EIntegrationDiTests
{
    // DI-friendly MagikUser that prepares books and calls the agent
    public sealed class EndToEndDiUser : MagikUser<ChangeRequest, string>
    {
        private readonly ICovenAgent<FixSpell, string> _agent;
        public EndToEndDiUser(ICovenAgent<FixSpell, string> agent)
        { _agent = agent; }

        protected override Task<string> InvokeAsync(
            ChangeRequest input,
            IBook<DefaultGuide> guide,
            IBook<DefaultSpell> spell,
            IBook<DefaultTest>  test,
            CancellationToken ct)
        {
            var payload = new FixSpell(
                guide.Payload.Markdown,
                spell.Payload.Version,
                test.Payload.Suite,
                input.Goal);
            var ctx = new SpellContext
            {
                ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
                Permissions = AgentPermissions.None()
            };
            return _agent.CastSpellAsync(payload, ctx, ct);
        }
    }

    [Fact]
    public async Task Pipeline_Composes_Via_DI_And_Runs()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);

        var services = new ServiceCollection();
        // Register agent as a service
        services.AddSingleton<ICovenAgent<FixSpell, string>, FakeAgent>();

        // Compose blocks via DI builder
        services.BuildCoven(c =>
        {
            c.AddBlock<ChangeRequest, string, EndToEndDiUser>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(temp, "di-demo"));

        Assert.Contains("di-demo", result);
        Assert.Contains("0.1", result);
        Assert.Contains("smoke", result);
        Assert.Contains(Path.GetFullPath(temp).TrimEnd(Path.DirectorySeparatorChar), result);
    }
}
