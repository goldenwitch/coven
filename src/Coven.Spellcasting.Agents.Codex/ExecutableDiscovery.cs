namespace Coven.Spellcasting.Agents.Codex;

internal static class ExecutableDiscovery
{
    // Prefer selecting a concrete shim on Windows to avoid extensionless/bare files
    public static string? TryLocateCodexOnWindowsPath()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var output = TryRunCapture("cmd.exe", "/c where codex");
            if (string.IsNullOrWhiteSpace(output)) return null;
            // Pick a .cmd first
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim())
                              .Where(l => !string.IsNullOrWhiteSpace(l))
                              .ToList();
            var cmd = lines.FirstOrDefault(l => l.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(cmd) && File.Exists(cmd)) return cmd;
            // Then .exe if any
            var exe = lines.FirstOrDefault(l => l.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe)) return exe;
            return null;
        }
        catch { return null; }
    }

    public static string? TryLocateCodexViaNpm()
    {
        try
        {
            // Ask npm for the global bin directory
            var bin = TryRunCapture("npm", "bin -g");
            if (string.IsNullOrWhiteSpace(bin) && OperatingSystem.IsWindows())
            {
                bin = TryRunCapture("cmd.exe", "/c npm bin -g");
            }
            bin = bin?.Trim();
            if (string.IsNullOrWhiteSpace(bin) || !Directory.Exists(bin!)) return null;

            if (OperatingSystem.IsWindows())
            {
                // Prefer .cmd shim; avoid .ps1 (PowerShell policy) and bare file without extension
                var cmd = Path.Combine(bin!, "codex.cmd");
                if (File.Exists(cmd)) return cmd;
                return null;
            }
            else
            {
                var exe = Path.Combine(bin!, "codex");
                if (File.Exists(exe)) return exe;
                return null;
            }
        }
        catch { return null; }
    }

    private static string? TryRunCapture(string fileName, string? arguments, int timeoutMs = 3000)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(fileName, arguments ?? string.Empty)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            // Carry PATH from current process
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv)) psi.Environment["PATH"] = pathEnv;
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(timeoutMs);
            return p.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }
}
