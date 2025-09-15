// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Tail;
using Coven.Spellcasting.Agents.Codex;
using System.Text;

namespace Coven.Toys.RolloutMuxConsole;

internal static class Program
{
    static class Config
    {
        // Run Codex via the npm PowerShell shim found in %APPDATA%\npm per scratch.txt
        public static string ExecutableName = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm",
            "codex.cmd");
        public const bool AugmentPathForCodex = false;
        public const char CommandEscape = '`';
        public const bool PrintStartupHelp = true;
        // Command-line arguments to pass to the executable (computed at runtime).
        public static string[] ExecutableArgs = { };
    }

    public static async Task<int> Main(string[] _)
    {
        // User Config (Toy): tweak these to change behavior while experimenting.
        // - ExecutableName: how to invoke Codex (must be on PATH or absolute).
        // - AugmentPathForCodex: prepend common install folders (e.g., %APPDATA%\npm on Windows).
        // - CommandEscape: key that enters command mode (double-tap for literal passthrough).
        // - PrintStartupHelp: whether to print the interactive help banner.


        // Launch Codex from PATH (no shim hardcoding).
        string exe = Config.ExecutableName;
        string ws = Directory.GetCurrentDirectory();
        await using CodexSessionScope session = new(ws);

        // Environment to match CodexCliAgent behavior per scratch.txt
        Dictionary<string, string?> env = new()
        {
            ["CODEX_HOME"] = session.CodexHome,
            ["CODEX_TUI_RECORD_SESSION"] = "1",
            ["CODEX_TUI_SESSION_LOG_PATH"] = session.RolloutPath
        };

        // Using PowerShell to run the npm Codex shim from %APPDATA%\npm\codex.ps1.

        await using ProcessSendPort send = new(
            fileName: exe,
            arguments: Config.ExecutableArgs,
            workingDirectory: ws,
            environment: env,
            configurePsi: psi => { if (Config.AugmentPathForCodex) psi.AugmentPathForCodex(); });

        await using DocumentTailSource tail = new(session.RolloutPath);

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Debug echo is compiled-in for DEBUG builds (see KeyDebugEcho.cs).

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

        // Eagerly start Codex after tail is wired so rollout begins without initial input
        await send.SafeWriteAsync(string.Empty, cts.Token).ConfigureAwait(false);

        // Console key pump -> Codex stdin (raw, with escape-hatch)
        var pump = new ConsoleKeyPump(send, Config.CommandEscape);
        Task inputTask = pump.RunAsync(cts.Token);

        // Print small help
        if (Config.PrintStartupHelp)
        {
            Console.WriteLine("RolloutMuxConsole ready.");
            Console.WriteLine("- Raw key passthrough enabled (arrows, etc.)");
            Console.WriteLine($"- Press {Config.CommandEscape} then type a command (help, exit, quit)");
            Console.WriteLine($"- Ctrl+C exits the mux; use {Config.CommandEscape}exit to send Ctrl+C to child");
        }

        try { await Task.WhenAll(tailTask, inputTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        return 0;
    }
}
