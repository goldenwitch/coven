using System.Diagnostics;

namespace Coven.Spellcasting.Agents.Codex.Processes;

internal sealed class DefaultCodexProcessFactory : ICodexProcessFactory
{
    private sealed class Handle : IProcessHandle
    {
        public Process Process { get; }
        public Handle(Process p) => Process = p;
        public async ValueTask DisposeAsync()
        {
            try { if (!Process.HasExited) Process.Kill(entireProcessTree: true); } catch { }
            try { await Process.WaitForExitAsync(); } catch { }
            Process.Dispose();
        }
    }

    public IProcessHandle Start(string executablePath, string workingDirectory, IReadOnlyDictionary<string, string?> environment)
    {
        var psi = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true
        };
        foreach (var kv in environment)
        {
            if (kv.Value is null) psi.Environment.Remove(kv.Key);
            else psi.Environment[kv.Key] = kv.Value;
        }
        var p = new Process { StartInfo = psi };
        if (!p.Start()) throw new InvalidOperationException("Failed to start Codex CLI process.");
        return new Handle(p);
    }
}

