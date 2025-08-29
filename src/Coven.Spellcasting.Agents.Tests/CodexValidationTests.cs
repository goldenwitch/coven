using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public class CodexValidationTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _dirs = new();

    private string NewTempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "coven-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(d);
        _dirs.Add(d);
        return d;
    }

    [Fact]
    public async Task Skips_Without_RunCommand_Permissions()
    {
        var opts = new CodexCliValidation.Options
        {
            // Probe false to force provisioning path
            ProbeAsync = (ctx, ct) => Task.FromResult(false),
            InstallerAsync = (ctx, ct) => Task.CompletedTask,
            StampDirectory = NewTempDir()
        };

        var v = new CodexCliValidation(opts);
        var ctx = new SpellContext { Permissions = AgentPermissions.None() };

        var res = await v.ValidateAsync(ctx);
        Assert.Equal(Agents.Validation.AgentValidationOutcome.Skipped, res.Outcome);
    }

    [Fact]
    public async Task Runs_Installer_When_Permissions_Allow()
    {
        var called = 0;
        var opts = new CodexCliValidation.Options
        {
            ProbeAsync = (ctx, ct) => Task.FromResult(false),
            InstallerAsync = (ctx, ct) => { called++; return Task.CompletedTask; },
            StampDirectory = NewTempDir()
        };
        var v = new CodexCliValidation(opts);
        var ctx = new SpellContext { Permissions = AgentPermissions.FullAuto() };

        var res1 = await v.ValidateAsync(ctx);
        var res2 = await v.ValidateAsync(ctx);

        Assert.Equal(Agents.Validation.AgentValidationOutcome.Performed, res1.Outcome);
        Assert.Equal(Agents.Validation.AgentValidationOutcome.Noop, res2.Outcome);
        Assert.Equal(1, called);
    }

    public void Dispose()
    {
        foreach (var d in _dirs)
        {
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }
        // Clean up Codex default stamp directory (best-effort)
        try
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(root, "Coven", "agents", "codex");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch { /* ignore */ }
    }
}
