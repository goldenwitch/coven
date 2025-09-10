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
            // Try direct start (native executables)
            if (TryStart(executablePath, null, workingDirectory, environment, out var handle))
                return handle!;

            // Windows fallback for .cmd/.bat npm shims
            if (OperatingSystem.IsWindows())
            {
                var cmdArgs = "/c " + BuildCommandLine(executablePath, null);
                if (TryStart("cmd.exe", cmdArgs, workingDirectory, environment, out var viaCmd))
                    return viaCmd!;
            }

            // npm global bin discovery: prefer codex from npm bin -g
            var npmCodex = ExecutableDiscovery.TryLocateCodexViaNpm();
            if (!string.IsNullOrWhiteSpace(npmCodex))
            {
                if (TryStart(npmCodex!, null, workingDirectory, environment, out var viaNpm))
                    return viaNpm!;
                if (OperatingSystem.IsWindows())
                {
                    var cmdArgsNpm = "/c " + BuildCommandLine(npmCodex!, null);
                    if (TryStart("cmd.exe", cmdArgsNpm, workingDirectory, environment, out var viaNpmCmd))
                        return viaNpmCmd!;
                }
            }

            // Windows PATH discovery via 'where' (prefer .cmd)
            var winCodex = ExecutableDiscovery.TryLocateCodexOnWindowsPath();
            if (!string.IsNullOrWhiteSpace(winCodex))
            {
                if (TryStart(winCodex!, null, workingDirectory, environment, out var viaWin))
                    return viaWin!;
                if (OperatingSystem.IsWindows())
                {
                    var cmdArgsWin = "/c " + BuildCommandLine(winCodex!, null);
                    if (TryStart("cmd.exe", cmdArgsWin, workingDirectory, environment, out var viaWinCmd))
                        return viaWinCmd!;
                }
            }

            throw new InvalidOperationException("Failed to start Codex CLI process.");
        }

        private static bool TryStart(
            string fileName,
            string? arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string?> environment,
            out IProcessHandle? handle)
        {
            try
            {
                var psi = new ProcessStartInfo(fileName, arguments ?? string.Empty)
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
                // Ensure PATH is explicitly carried from current process
                var pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrWhiteSpace(pathEnv))
                    psi.Environment["PATH"] = pathEnv;
                var p = new Process { StartInfo = psi };
                if (!p.Start()) { handle = null; return false; }
                handle = new Handle(p);
                return true;
            }
            catch
            {
                handle = null;
                return false;
            }
        }

        private static string BuildCommandLine(string fileName, string? arguments)
        {
            var needsQuotes = fileName.Contains(' ') && !fileName.StartsWith('"') && !fileName.EndsWith('"');
            var quoted = needsQuotes ? $"\"{fileName}\"" : fileName;
            return string.IsNullOrWhiteSpace(arguments) ? quoted : $"{quoted} {arguments}";
        }
}
