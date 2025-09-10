using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.Validation;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class ValidationExecutorTests
{
    private sealed class DummySpell : ISpell
    {
        public Task CastSpell() => Task.CompletedTask;
    }
    private sealed class FakeOps : IValidationOps
    {
        public int RunCalls;
        public int EnsureDirCalls;
        public int WriteCalls;
        public int DeleteCalls;
        public int PipeCalls;
        public int MergeCalls;
        public bool RunResult = true;
        public bool ThrowOnPipe = false;
        public bool FileExistsResult = true;

        public ProcessRunResult RunProcess(string fileName, string? arguments, string workingDirectory, IReadOnlyDictionary<string, string?>? environment)
        {
            RunCalls++;
            return new ProcessRunResult(RunResult, "ok");
        }
        public bool FileExists(string path) => FileExistsResult;
        
        public void EnsureDirectory(string path) => EnsureDirCalls++;
        public Task WriteFileAsync(string path, string contents, CancellationToken ct) { WriteCalls++; return Task.CompletedTask; }
        public void DeleteFile(string path) => DeleteCalls++;
        public void PipeHandshake(string pipeName, CancellationToken ct) { PipeCalls++; if (ThrowOnPipe) throw new InvalidOperationException("pipe fail"); }
        public void MergeConfig(ICodexConfigWriter writer, string codexHomeDir, string shimPath, string pipeName, string serverKey) => MergeCalls++;
    }

    private sealed class NoopWriter : ICodexConfigWriter
    {
        public void WriteOrMerge(string codexHomeDir, string shimPath, string pipeName, string serverKey = "coven") { }
    }

    [Fact]
    public async Task Validate_Succeeds_With_Fakes_And_Records_Calls()
    {
        var ops = new FakeOps();
        var writer = new NoopWriter();
        var v = new CodexCliValidation(
            executablePath: "codex",
            workspaceDirectory: Path.Combine(Path.GetTempPath(), $"coven_ws_{Guid.NewGuid():N}"),
            shimExecutablePath: "shim.exe",
            spellbook: new SpellbookBuilder().AddSpell(new DummySpell()).Build(),
            configWriter: writer,
            ops: ops);

        var result = await v.ValidateAsync();

        Assert.True(ops.RunCalls >= 2); // version + sessions (+ shim help)
        Assert.True(ops.EnsureDirCalls >= 2); // workspace + codex home
        Assert.True(ops.PipeCalls >= 1);
        Assert.True(ops.MergeCalls >= 1);
        Assert.True(result.Outcome == Coven.Spellcasting.Agents.Validation.AgentValidationOutcome.Performed || result.Outcome == Coven.Spellcasting.Agents.Validation.AgentValidationOutcome.Noop);
    }

    [Fact]
    public async Task Validate_Fails_When_Codex_Run_Fails()
    {
        var ops = new FakeOps { RunResult = false };
        var writer = new NoopWriter();
        var v = new CodexCliValidation(
            executablePath: "codex",
            workspaceDirectory: Path.Combine(Path.GetTempPath(), $"coven_ws_{Guid.NewGuid():N}"),
            shimExecutablePath: null,
            spellbook: new SpellbookBuilder().Build(),
            configWriter: writer,
            ops: ops);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await v.ValidateAsync());
    }
}
