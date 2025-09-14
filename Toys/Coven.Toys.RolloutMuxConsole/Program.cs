// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Tail;

namespace Coven.Toys.RolloutMuxConsole;

    internal static class Config
    {
    // Path to the Codex CLI executable (computed by OS)
    // - Windows: %AppData%\npm\codex.cmd
    // - Non-Windows: "codex" (resolve via PATH)
    public static string ExecutablePath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "codex.cmd")
        : "codex";

    // Optional workspace directory; null uses current directory
    public static string? WorkspaceDirectory = null;

        // Enable verbose PATH dump
        public static bool Debug = false;
    }

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string exe = Config.ExecutablePath;
        string ws  = Config.WorkspaceDirectory ?? Directory.GetCurrentDirectory();
        string codexHome = Path.Combine(ws, ".codex");
        try { Directory.CreateDirectory(codexHome); } catch { }

        string rolloutPath = Path.Combine(codexHome, "codex.rollout.jsonl");

        // Environment and args to mirror DefaultTailMuxFactory behavior
        Dictionary<string, string?> env = new() { ["CODEX_HOME"] = codexHome };
        string[] argsList = ["--log-dir", codexHome];

        // Debug environment info for PATH/npx visibility
        if (Config.Debug)
            PrintDebugInfo(exe, ws, codexHome);

        await using ProcessSendPort send = new(
            fileName: exe,
            arguments: argsList,
            workingDirectory: ws,
            environment: env);

        await using DocumentTailSource tail = new(rolloutPath);

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Tail Codex rollout -> console
        Task tailTask = tail.TailAsync(ev =>
        {
            switch (ev)
            {
                case Line o:
                    Console.WriteLine(o.Line);
                    break;
                case ErrorLine e:
                    Console.Error.WriteLine(e.Line);
                    break;
            }
            return ValueTask.CompletedTask;
        }, cts.Token);

        // Console input -> Codex stdin
        Task inputTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                string? line;
                try { line = await Console.In.ReadLineAsync().WaitAsync(cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (line is null) break; // EOF

                try { await send.WriteLineAsync(line, cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (FileNotFoundException fnf)
                {
                    Console.Error.WriteLine($"[codex-missing] {fnf.Message}");
                    break;
                }
                catch (DirectoryNotFoundException dnf)
                {
                    Console.Error.WriteLine($"[workspace-invalid] {dnf.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[write-error] {ex.Message}");
                    break;
                }
            }
        }, cts.Token);

        // Print small help
        Console.WriteLine("RolloutMuxConsole ready. Ctrl+C to exit.");
        if (Config.Debug)
        {
            Console.WriteLine($"Workspace: {ws}");
            Console.WriteLine($"CodexHome: {codexHome}");
            Console.WriteLine($"Executable: {exe} {string.Join(" ", argsList)}");
        }

        try { await Task.WhenAll(tailTask, inputTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        return 0;
    }

    private static void PrintDebugInfo(string exe, string ws, string codexHome)
    {
        Console.WriteLine("=== RolloutMux Debug ===");
        Console.WriteLine($"Exe: {exe}");
        Console.WriteLine($"WS: {ws}");
        Console.WriteLine($"Home: {codexHome}");
        Console.WriteLine($"OS: {Environment.OSVersion} 64bit:{Environment.Is64BitProcess}");

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathext = Environment.GetEnvironmentVariable("PATHEXT");
        Console.WriteLine($"PATH length: {path.Length}");
        if (!string.IsNullOrWhiteSpace(pathext))
            Console.WriteLine($"PATHEXT: {pathext}");

        // Common Node managers and hints
        (string Key, string? Val)[] hints =
        [
            ("NVM_BIN", Environment.GetEnvironmentVariable("NVM_BIN")),
            ("NVM_DIR", Environment.GetEnvironmentVariable("NVM_DIR")),
            ("VOLTA_HOME", Environment.GetEnvironmentVariable("VOLTA_HOME")),
            ("FNM_DIR", Environment.GetEnvironmentVariable("FNM_DIR")),
            ("ASDF_DATA_DIR", Environment.GetEnvironmentVariable("ASDF_DATA_DIR")),
            ("npm_config_prefix", Environment.GetEnvironmentVariable("npm_config_prefix")),
        ];
        foreach (var (k, v) in hints)
        {
            if (!string.IsNullOrWhiteSpace(v)) Console.WriteLine($"{k}: {v}");
        }

        Console.WriteLine("-- PATH entries --");
        foreach (var entry in path.Split(Path.PathSeparator))
        {
            Console.WriteLine(entry);
        }
        Console.WriteLine("-- End PATH entries --");
    }
}
